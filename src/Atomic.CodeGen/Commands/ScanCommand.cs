using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
		command.SetHandler(async (string projectPath, bool verbose) =>
		{
			await ExecuteAsync(projectPath, verbose);
		}, projectOption, verboseOption);
		return command;
	}

	private static async Task ExecuteAsync(string projectPath, bool verbose)
	{
		Logger.SetVerbose(verbose);
		Logger.LogHeader("Atomic CodeGen - Scan");
		CodeGenConfig config = await ConfigLoader.LoadAsync(projectPath);
		config.Verbose = verbose;
		string solutionPath = FindSolutionFile(projectPath);
		DiscoveryResult discoveryResult = null;

		if (solutionPath != null)
		{
			Logger.LogInfo("Using Roslyn semantic analysis...");
			Logger.LogInfo("");
			using SemanticTypeDiscovery discovery = new SemanticTypeDiscovery(solutionPath, config.AnalyzerMode, config.IncludedProjects);
			discoveryResult = await discovery.DiscoverAllAsync(config.ExcludePaths);
		}

		if (discoveryResult != null && (discoveryResult.EntityApis.Count > 0 || discoveryResult.Behaviours.Count > 0 || discoveryResult.Domains.Count > 0))
		{
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
						string constructorParams = string.Join(", ", behaviour.ConstructorParameters.Select(p => $"{p.Type} {p.Name}"));
						Logger.LogInfo("    Constructor: (" + constructorParams + ")");
					}
					Logger.LogInfo("");
				}
			}
			if (discoveryResult.Domains.Count > 0)
			{
				AnsiConsole.Write(new Rule("[bold blue]Entity Domains[/]"));
				AnsiConsole.WriteLine();
				foreach (var (_, definition) in discoveryResult.Domains)
				{
					Logger.LogSuccess("✓ " + definition.EntityName);
					Logger.LogInfo("    Class: " + definition.ClassName);
					Logger.LogInfo("    Namespace: " + definition.Namespace);
					Logger.LogInfo("    Directory: " + definition.Directory);
					Logger.LogInfo($"    Mode: {definition.Mode}");
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
			if (solutionPath != null)
			{
				Logger.LogWarning("Semantic analysis found no definitions, falling back to file scanning...");
			}
			else
			{
				Logger.LogInfo("No solution file found, using file scanning...");
			}
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
	}

	private static string? FindSolutionFile(string projectPath)
	{
		return Directory.GetFiles(projectPath, "*.sln", SearchOption.TopDirectoryOnly)
			.Concat(Directory.GetFiles(projectPath, "*.slnx", SearchOption.TopDirectoryOnly))
			.FirstOrDefault();
	}
}
