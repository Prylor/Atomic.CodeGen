using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Atomic.CodeGen.Core.Generators;
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
		Option<string> projectOption = new Option<string>(["--project", "-p"], Directory.GetCurrentDirectory, "Path to project root");
		Option<bool> verboseOption = new Option<bool>(["--verbose", "-v"], () => false, "Enable verbose logging");
		Command command = new Command("generate", "Generate Entity API files") { projectOption, verboseOption };
		command.SetHandler(async (string projectPath, bool verbose) =>
		{
			await ExecuteAsync(projectPath, verbose);
		}, projectOption, verboseOption);
		return command;
	}

	private static async Task ExecuteAsync(string projectPath, bool verbose)
	{
		Logger.SetVerbose(verbose);
		Logger.LogHeader("Atomic CodeGen - Generate");

		Stopwatch stopwatch = Stopwatch.StartNew();
		CodeGenConfig config = await ConfigLoader.LoadAsync(projectPath);
		config.Verbose = verbose;

		var (definitions, allBehaviours, domainDefinitions) = await DiscoverDefinitionsAsync(projectPath, config);

		LinkBehavioursToDefinitions(definitions, allBehaviours);

		Logger.LogInfo($"Found {domainDefinitions.Count} Entity Domain definitions");
		Logger.LogInfo("");

		HashSet<string> expectedOutputPaths = CollectExpectedOutputPaths(definitions, domainDefinitions, config);

		Logger.LogInfo("Checking for orphaned generated files...");
		int orphanedCount = await OrphanedFilesCleaner.CleanOrphanedFilesAsync(config, config.ScanPaths, expectedOutputPaths);
		if (orphanedCount > 0)
		{
			Logger.LogInfo($"Cleaned {orphanedCount} orphaned file(s)");
		}
		Logger.LogInfo("");

		if (definitions.Count == 0 && domainDefinitions.Count == 0)
		{
			Logger.LogWarning("No definitions found!");
			Logger.LogInfo("Make sure your classes are marked with [EntityAPI] or implement IEntityDomain");
			return;
		}

		int apiGeneratedCount = await GenerateEntityApis(definitions, config);
		int domainGeneratedCount = await GenerateEntityDomains(domainDefinitions, config);

		stopwatch.Stop();
		LogSummary(apiGeneratedCount, definitions.Count, domainGeneratedCount, domainDefinitions.Count, stopwatch.ElapsedMilliseconds);
	}

	private static async Task<(
		List<(string filePath, EntityAPIDefinition definition)> definitions,
		List<BehaviourDefinition> behaviours,
		Dictionary<string, EntityDomainDefinition> domains
	)> DiscoverDefinitionsAsync(string projectPath, CodeGenConfig config)
	{
		var definitions = new List<(string filePath, EntityAPIDefinition definition)>();
		var allBehaviours = new List<BehaviourDefinition>();
		var domainDefinitions = new Dictionary<string, EntityDomainDefinition>();

		string solutionPath = FindSolutionFile(projectPath);

		if (solutionPath != null)
		{
			Logger.LogInfo("Using Roslyn semantic analysis for type discovery...");
			using SemanticTypeDiscovery discovery = new SemanticTypeDiscovery(solutionPath, config.AnalyzerMode, config.IncludedProjects);
			DiscoveryResult result = await discovery.DiscoverAllAsync(config.ExcludePaths);

			definitions = result.EntityApis
				.Where(d => d.IsValid)
				.Select(d => (d.SourceFile, d))
				.ToList();
			allBehaviours = result.Behaviours;
			domainDefinitions = result.Domains;
		}
		else
		{
			Logger.LogInfo("No solution file found, using file scanning...");
			List<string> scannedFiles = new FileScanner(config).Scan();
			Logger.LogInfo($"Scanning {scannedFiles.Count} files...");

			EntityAPIParser parser = new EntityAPIParser();
			foreach (string file in scannedFiles)
			{
				EntityAPIDefinition definition = await parser.ParseFileAsync(file);
				if (definition != null && definition.IsValid)
				{
					definitions.Add((file, definition));
				}
			}

			BehaviourParser behaviourParser = new BehaviourParser();
			List<string> behaviourFiles = FileScanner.FindFiles(config.GetAbsoluteProjectRoot(), "Assets/**/*.cs", config.ExcludePaths);
			foreach (string behaviourFile in behaviourFiles)
			{
				allBehaviours.AddRange(await behaviourParser.ParseFileAsync(behaviourFile));
			}

			domainDefinitions = await EntityDomainScanner.ScanAsync(config);
		}

		Logger.LogInfo($"Found {definitions.Count} Entity API definitions");
		return (definitions, allBehaviours, domainDefinitions);
	}

	private static void LinkBehavioursToDefinitions(
		List<(string filePath, EntityAPIDefinition definition)> definitions,
		List<BehaviourDefinition> allBehaviours)
	{
		foreach (var (_, definition) in definitions)
		{
			List<BehaviourDefinition> linked = allBehaviours
				.Where(b => b.LinkedApiTypeName == definition.ClassName
					|| b.LinkedApiTypeName == definition.Namespace + "." + definition.ClassName)
				.ToList();

			definition.LinkedBehaviours = linked;
			if (linked.Count > 0)
			{
				Logger.LogVerbose($"  {definition.ClassName}: {linked.Count} linked behaviour(s)");
			}
		}

		int totalLinked = definitions.Sum(d => d.definition.LinkedBehaviours.Count);
		if (totalLinked > 0)
		{
			Logger.LogInfo($"Found {totalLinked} linked behaviour(s)");
		}
	}

	private static HashSet<string> CollectExpectedOutputPaths(
		List<(string filePath, EntityAPIDefinition definition)> definitions,
		Dictionary<string, EntityDomainDefinition> domainDefinitions,
		CodeGenConfig config)
	{
		HashSet<string> paths = new HashSet<string>();

		foreach (var (_, definition) in definitions)
		{
			string outputFilePath = definition.GetOutputFilePath(config);
			paths.Add(Path.GetFullPath(outputFilePath));
		}

		string absoluteProjectRoot = config.GetAbsoluteProjectRoot();
		foreach (var (_, domainDef) in domainDefinitions)
		{
			foreach (string expectedFilePath in EntityDomainFileHelper.GetExpectedFilePaths(domainDef, absoluteProjectRoot))
			{
				paths.Add(Path.GetFullPath(expectedFilePath));
			}
		}

		return paths;
	}

	private static async Task<int> GenerateEntityApis(
		List<(string filePath, EntityAPIDefinition definition)> definitions,
		CodeGenConfig config)
	{
		int generatedCount = 0;
		foreach (var (_, definition) in definitions)
		{
			try
			{
				if (await new EntityAPIGenerator(definition, config).GenerateAsync())
				{
					generatedCount++;
				}
			}
			catch (Exception ex)
			{
				Logger.LogError($"Failed to generate {definition.ClassName}: {ex.Message}");
			}
		}
		return generatedCount;
	}

	private static async Task<int> GenerateEntityDomains(
		Dictionary<string, EntityDomainDefinition> domainDefinitions,
		CodeGenConfig config)
	{
		if (domainDefinitions.Count == 0)
			return 0;

		Logger.LogInfo("");
		int generatedCount = 0;
		foreach (var (_, domainDef) in domainDefinitions)
		{
			try
			{
				if (await new EntityDomainOrchestrator(domainDef, config).GenerateAsync())
				{
					generatedCount++;
				}
			}
			catch (Exception ex)
			{
				Logger.LogError($"Failed to generate EntityDomain {domainDef.EntityName}: {ex.Message}");
			}
		}
		return generatedCount;
	}

	private static void LogSummary(int apiCount, int apiTotal, int domainCount, int domainTotal, long elapsedMs)
	{
		Logger.LogInfo("");
		Logger.LogInfo("═══════════════════════════════════════════════════════");

		if (apiCount > 0)
		{
			Logger.LogSuccess($"Entity API: {apiCount}/{apiTotal} files");
		}
		if (domainCount > 0)
		{
			Logger.LogSuccess($"Entity Domains: {domainCount}/{domainTotal} domains");
		}

		int total = apiCount + domainCount;
		int totalDefs = apiTotal + domainTotal;
		Logger.LogSuccess($"Total: {total}/{totalDefs} in {elapsedMs}ms");
		Console.ResetColor();
	}

	private static string? FindSolutionFile(string projectPath)
	{
		return Directory.GetFiles(projectPath, "*.sln", SearchOption.TopDirectoryOnly)
			.Concat(Directory.GetFiles(projectPath, "*.slnx", SearchOption.TopDirectoryOnly))
			.FirstOrDefault();
	}
}
