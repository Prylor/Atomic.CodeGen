using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using Atomic.CodeGen.Core.Models;
using Atomic.CodeGen.Roslyn;
using Atomic.CodeGen.Utils;
using Spectre.Console;

namespace Atomic.CodeGen.Commands;

public static class ScanCommand
{
	public static Command Create()
	{
		Option<string> projectOption = new Option<string>(["--project", "-p"], () => Directory.GetCurrentDirectory(), "Path to project root");
		Option<bool> verboseOption = new Option<bool>(["--verbose", "-v"], () => false, "Enable verbose logging");
		Command command = new Command("scan", "Scan for Entity API definitions") { projectOption, verboseOption };
		command.SetHandler(async delegate(string projectPath, bool verbose)
		{
			Logger.SetVerbose(verbose);
			Logger.LogHeader("Atomic CodeGen - Scan");
			CodeGenConfig config = await ConfigLoader.LoadAsync(projectPath);
			config.Verbose = verbose;
			string solutionPath = FindSolutionFile(projectPath);
			if (solutionPath != null)
			{
				Logger.LogInfo("Using Roslyn semantic analysis...");
				Logger.LogInfo("");
				using SemanticTypeDiscovery discovery = new SemanticTypeDiscovery(solutionPath, config.AnalyzerMode, config.IncludedProjects);
				DiscoveryResult discoveryResult = await discovery.DiscoverAllAsync(config.ExcludePaths);
				if (discoveryResult.EntityApis.Count > 0)
				{
					AnsiConsole.Write(new Rule("[bold blue]Entity APIs[/]"));
					AnsiConsole.WriteLine();
					foreach (EntityAPIDefinition entityApi in discoveryResult.EntityApis)
					{
						Logger.LogSuccess("✓ " + entityApi.ClassName);
						Logger.LogInfo("    Namespace: " + entityApi.Namespace);
						Logger.LogInfo("    Source: " + Path.GetRelativePath(projectPath, entityApi.SourceFile));
						Logger.LogInfo($"    Tags: {entityApi.Tags.Count}, Values: {entityApi.Values.Count}");
						Logger.LogInfo("");
					}
				}
				if (discoveryResult.Behaviours.Count > 0)
				{
					AnsiConsole.Write(new Rule("[bold blue]Behaviours[/]"));
					AnsiConsole.WriteLine();
					foreach (BehaviourDefinition behaviour in discoveryResult.Behaviours)
					{
						Logger.LogSuccess("✓ " + behaviour.ClassName);
						Logger.LogInfo("    Linked to: " + behaviour.LinkedApiTypeName);
						Logger.LogInfo("    Source: " + Path.GetRelativePath(projectPath, behaviour.SourceFile));
						if (behaviour.ConstructorParameters.Count > 0)
						{
							string constructorParams = string.Join(", ", behaviour.ConstructorParameters.Select<(string, string), string>(((string Name, string Type) p) => p.Type + " " + p.Name));
							Logger.LogInfo("    Constructor: (" + constructorParams + ")");
						}
						Logger.LogInfo("");
					}
				}
				if (discoveryResult.Domains.Count > 0)
				{
					AnsiConsole.Write(new Rule("[bold blue]Entity Domains[/]"));
					AnsiConsole.WriteLine();
					foreach (var (_, entityDomainDefinition2) in discoveryResult.Domains)
					{
						Logger.LogSuccess("✓ " + entityDomainDefinition2.EntityName);
						Logger.LogInfo("    Class: " + entityDomainDefinition2.ClassName);
						Logger.LogInfo("    Namespace: " + entityDomainDefinition2.Namespace);
						Logger.LogInfo("    Directory: " + entityDomainDefinition2.Directory);
						Logger.LogInfo($"    Mode: {entityDomainDefinition2.Mode}");
						Logger.LogInfo("");
					}
				}
				Logger.LogInfo("");
				AnsiConsole.Write(new Rule("[bold green]Summary[/]"));
				Table table = new Table().Border(TableBorder.Rounded).AddColumn("[bold]Type[/]").AddColumn("[bold]Count[/]");
				table.AddRow("Entity APIs", discoveryResult.EntityApis.Count.ToString());
				table.AddRow("Behaviours", discoveryResult.Behaviours.Count.ToString());
				table.AddRow("Entity Domains", discoveryResult.Domains.Count.ToString());
				AnsiConsole.Write(table);
			}
			else
			{
				Logger.LogInfo("No solution file found, using file scanning...");
				Logger.LogInfo("");
				List<string> scannedFiles = new FileScanner(config).Scan();
				Logger.LogInfo($"Scanning {scannedFiles.Count} files...");
				Logger.LogInfo("");
				EntityAPIParser parser = new EntityAPIParser();
				List<string> definitions = new List<string>();
				foreach (string file in scannedFiles)
				{
					EntityAPIDefinition entityAPIDefinition = await parser.ParseFileAsync(file);
					if (entityAPIDefinition != null && entityAPIDefinition.IsValid)
					{
						definitions.Add(entityAPIDefinition.ClassName);
						Logger.LogSuccess("✓ " + entityAPIDefinition.ClassName);
						Logger.LogInfo("    Source: " + Path.GetRelativePath(projectPath, file));
						Logger.LogInfo("    Output: " + Path.GetRelativePath(projectPath, entityAPIDefinition.GetOutputFilePath(config)));
						Logger.LogInfo($"    Tags: {entityAPIDefinition.Tags.Count}, Values: {entityAPIDefinition.Values.Count}");
						Logger.LogInfo("");
					}
				}
				Logger.LogInfo("═══════════════════════════════════════════════════════");
				Logger.LogSuccess($"Found {definitions.Count} valid Entity API definitions");
			}
			Console.ResetColor();
		}, projectOption, verboseOption);
		return command;
	}

	private static string? FindSolutionFile(string projectPath)
	{
		return Directory.GetFiles(projectPath, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();
	}
}
