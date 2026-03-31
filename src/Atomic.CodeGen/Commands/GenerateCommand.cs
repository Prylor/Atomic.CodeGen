using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Atomic.CodeGen.Core;
using Atomic.CodeGen.Core.Generators.EntityDomain;
using Atomic.CodeGen.Core.Models;
using Atomic.CodeGen.Core.Models.EntityDomain;
using Atomic.CodeGen.Core.Scanners;
using Atomic.CodeGen.Roslyn;
using Atomic.CodeGen.Utils;

namespace Atomic.CodeGen.Commands;

public static class GenerateCommand
{
	public static Command Create()
	{
		Option<string> option = new Option<string>(new string[2] { "--project", "-p" }, Directory.GetCurrentDirectory, "Path to project root");
		Option<bool> option2 = new Option<bool>(new string[2] { "--verbose", "-v" }, () => false, "Enable verbose logging");
		Command obj = new Command("generate", "Generate Entity API files") { option, option2 };
		obj.SetHandler(async delegate(string projectPath, bool verbose)
		{
			Logger.SetVerbose(verbose);
			Logger.LogHeader("Atomic CodeGen - Generate");
			Stopwatch stopwatch = Stopwatch.StartNew();
			CodeGenConfig config = await ConfigLoader.LoadAsync(projectPath);
			config.Verbose = verbose;
			string text = FindSolutionFile(projectPath);
			List<(string filePath, EntityAPIDefinition definition)> definitions = new List<(string, EntityAPIDefinition)>();
			List<BehaviourDefinition> allBehaviours = new List<BehaviourDefinition>();
			Dictionary<string, EntityDomainDefinition> domainDefinitions = new Dictionary<string, EntityDomainDefinition>();
			if (text != null)
			{
				Logger.LogInfo("Using Roslyn semantic analysis for type discovery...");
				using SemanticTypeDiscovery discovery = new SemanticTypeDiscovery(text, config.AnalyzerMode, config.IncludedProjects);
				DiscoveryResult discoveryResult = await discovery.DiscoverAllAsync(config.ExcludePaths);
				definitions = (from d in discoveryResult.EntityApis
					where d.IsValid
					select (SourceFile: d.SourceFile, d: d)).ToList();
				allBehaviours = discoveryResult.Behaviours;
				domainDefinitions = discoveryResult.Domains;
				Logger.LogInfo($"Found {definitions.Count} Entity API definitions");
			}
			else
			{
				Logger.LogInfo("No solution file found, using file scanning...");
				List<string> list = new FileScanner(config).Scan();
				Logger.LogInfo($"Scanning {list.Count} files...");
				EntityAPIParser parser = new EntityAPIParser();
				foreach (string file in list)
				{
					EntityAPIDefinition entityAPIDefinition = await parser.ParseFileAsync(file);
					if (entityAPIDefinition != null && entityAPIDefinition.IsValid)
					{
						definitions.Add((file, entityAPIDefinition));
					}
				}
				Logger.LogInfo($"Found {definitions.Count} Entity API definitions");
				BehaviourParser behaviourParser = new BehaviourParser();
				List<string> list2 = FileScanner.FindFiles(config.GetAbsoluteProjectRoot(), "Assets/**/*.cs", config.ExcludePaths);
				foreach (string item in list2)
				{
					allBehaviours.AddRange(await behaviourParser.ParseFileAsync(item));
				}
				domainDefinitions = await EntityDomainScanner.ScanAsync(config);
			}
			foreach (var item2 in definitions)
			{
				EntityAPIDefinition definition = item2.definition;
				List<BehaviourDefinition> list3 = allBehaviours.Where((BehaviourDefinition b) => b.LinkedApiTypeName == definition.ClassName || b.LinkedApiTypeName == definition.Namespace + "." + definition.ClassName).ToList();
				definition.LinkedBehaviours = list3;
				if (list3.Count > 0)
				{
					Logger.LogVerbose($"  {definition.ClassName}: {list3.Count} linked behaviour(s)");
				}
			}
			int num = definitions.Sum<(string, EntityAPIDefinition)>(((string filePath, EntityAPIDefinition definition) d) => d.definition.LinkedBehaviours.Count);
			if (num > 0)
			{
				Logger.LogInfo($"Found {num} linked behaviour(s)");
			}
			Logger.LogInfo($"Found {domainDefinitions.Count} Entity Domain definitions");
			Logger.LogInfo("");
			HashSet<string> hashSet = new HashSet<string>();
			foreach (var item3 in definitions)
			{
				string outputFilePath = item3.definition.GetOutputFilePath(config);
				hashSet.Add(Path.GetFullPath(outputFilePath));
			}
			string absoluteProjectRoot = config.GetAbsoluteProjectRoot();
			string key;
			EntityDomainDefinition value;
			foreach (KeyValuePair<string, EntityDomainDefinition> item4 in domainDefinitions)
			{
				item4.Deconstruct(out key, out value);
				foreach (string expectedFilePath in EntityDomainFileHelper.GetExpectedFilePaths(value, absoluteProjectRoot))
				{
					hashSet.Add(Path.GetFullPath(expectedFilePath));
				}
			}
			Logger.LogInfo("Checking for orphaned generated files...");
			int num2 = await OrphanedFilesCleaner.CleanOrphanedFilesAsync(config, config.ScanPaths, hashSet);
			if (num2 > 0)
			{
				Logger.LogInfo($"Cleaned {num2} orphaned file(s)");
			}
			Logger.LogInfo("");
			if (definitions.Count == 0 && domainDefinitions.Count == 0)
			{
				Logger.LogWarning("No definitions found!");
				Logger.LogInfo("Make sure your classes are marked with [EntityAPI] or implement IEntityDomain");
			}
			else
			{
				int apiGeneratedCount = 0;
				if (definitions.Count > 0)
				{
					foreach (var item5 in definitions)
					{
						EntityAPIDefinition definition2 = item5.definition;
						try
						{
							if (await new EntityAPIGenerator(definition2, config).GenerateAsync())
							{
								apiGeneratedCount++;
							}
						}
						catch (Exception ex)
						{
							Logger.LogError("Failed to generate " + definition2.ClassName + ": " + ex.Message);
						}
					}
				}
				int domainGeneratedCount = 0;
				if (domainDefinitions.Count > 0)
				{
					Logger.LogInfo("");
					foreach (KeyValuePair<string, EntityDomainDefinition> item6 in domainDefinitions)
					{
						item6.Deconstruct(out key, out value);
						EntityDomainDefinition domainDef = value;
						try
						{
							if (await new EntityDomainOrchestrator(domainDef, config).GenerateAsync())
							{
								domainGeneratedCount++;
							}
						}
						catch (Exception ex2)
						{
							Logger.LogError("Failed to generate EntityDomain " + domainDef.EntityName + ": " + ex2.Message);
						}
					}
				}
				stopwatch.Stop();
				Logger.LogInfo("");
				Logger.LogInfo("═══════════════════════════════════════════════════════");
				int value2 = apiGeneratedCount + domainGeneratedCount;
				int value3 = definitions.Count + domainDefinitions.Count;
				if (apiGeneratedCount > 0)
				{
					Logger.LogSuccess($"Entity API: {apiGeneratedCount}/{definitions.Count} files");
				}
				if (domainGeneratedCount > 0)
				{
					Logger.LogSuccess($"Entity Domains: {domainGeneratedCount}/{domainDefinitions.Count} domains");
				}
				Logger.LogSuccess($"Total: {value2}/{value3} in {stopwatch.ElapsedMilliseconds}ms");
				Console.ResetColor();
			}
		}, option, option2);
		return obj;
	}

	private static string? FindSolutionFile(string projectPath)
	{
		return Directory.GetFiles(projectPath, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();
	}
}
