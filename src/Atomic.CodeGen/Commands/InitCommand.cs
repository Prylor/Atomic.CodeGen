using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Atomic.CodeGen.Core.Models;
using Atomic.CodeGen.Utils;
using Spectre.Console;

namespace Atomic.CodeGen.Commands;

public static class InitCommand
{
	public static Command Create()
	{
		Option<string> projectOption = new Option<string>(["--project", "-p"], () => Directory.GetCurrentDirectory(), "Path to project root");
		Option<bool> quickOption = new Option<bool>(["--quick", "-q"], () => false, "Skip interactive setup and use defaults");
		Command command = new Command("init", "Initialize configuration file (interactive)") { projectOption, quickOption };
		command.SetHandler(async (string projectPath, bool quick) =>
		{
			AnsiConsole.Write(new FigletText("Atomic CodeGen").LeftJustified().Color(Color.Blue));
			AnsiConsole.Write(new Rule("[bold blue]Configuration Setup[/]"));
			AnsiConsole.WriteLine();
			string configPath = Path.Combine(projectPath, "atomic-codegen.json");
			if (File.Exists(configPath))
			{
				AnsiConsole.MarkupLine("[yellow]Configuration file already exists:[/] " + Markup.Escape(configPath));
				if (!AnsiConsole.Confirm("Overwrite existing configuration?", defaultValue: false))
				{
					AnsiConsole.MarkupLine("[dim]Initialization cancelled[/]");
					return;
				}
				AnsiConsole.WriteLine();
			}
			CodeGenConfig config;
			if (quick)
			{
				config = CreateDefaultConfig(projectPath);
				AnsiConsole.MarkupLine("[dim]Using default configuration (quick mode)[/]");
			}
			else
			{
				config = await RunInteractiveSetupAsync(projectPath);
			}
			ShowConfigPreview(config);
			if (!quick && !AnsiConsole.Confirm("Save this configuration?"))
			{
				AnsiConsole.MarkupLine("[dim]Configuration not saved[/]");
			}
			else
			{
				await ConfigLoader.SaveAsync(config, projectPath);
				bool gitignoreUpdated = GitIgnoreHelper.EnsureRenameBackupIgnored(projectPath);
				AnsiConsole.WriteLine();
				AnsiConsole.Write(new Rule("[green]Setup Complete[/]"));
				AnsiConsole.MarkupLine("[green]✓[/] Configuration saved to: [blue]" + Markup.Escape(configPath) + "[/]");
				if (gitignoreUpdated)
				{
					AnsiConsole.MarkupLine("[green]✓[/] Added [blue].rename-backup/[/] to .gitignore");
				}
				AnsiConsole.MarkupLine("[dim]Run [blue]atomic-codegen generate[/] to generate code[/]");
				AnsiConsole.MarkupLine("[dim]Run [blue]atomic-codegen configure[/] to modify settings[/]");
			}
		}, projectOption, quickOption);
		return command;
	}

	private static Task<CodeGenConfig> RunInteractiveSetupAsync(string projectPath)
	{
		CodeGenConfig codeGenConfig = new CodeGenConfig
		{
			ProjectRoot = projectPath
		};
		AnsiConsole.MarkupLine("[bold]1. Logging[/]");
		codeGenConfig.Verbose = AnsiConsole.Confirm("Enable verbose logging by default?", defaultValue: false);
		AnsiConsole.WriteLine();
		AnsiConsole.MarkupLine("[bold]2. Orphan Tracking[/]");
		AnsiConsole.MarkupLine("[dim]Automatically clean up generated files when source is deleted[/]");
		codeGenConfig.TrackOrphans = AnsiConsole.Confirm("Enable orphan file tracking?");
		AnsiConsole.WriteLine();
		AnsiConsole.MarkupLine("[bold]3. Code Formatting[/]");
		string indentationChoice = AnsiConsole.Prompt(
			new SelectionPrompt<string>()
				.Title("Indentation style:")
				.AddChoices("Spaces (4)", "Spaces (2)", "Tabs"));
		codeGenConfig.Formatting = indentationChoice switch
		{
			"Spaces (4)" => new FormattingOptions
			{
				UseTabs = false,
				IndentSize = 4
			}, 
			"Spaces (2)" => new FormattingOptions
			{
				UseTabs = false,
				IndentSize = 2
			}, 
			"Tabs" => new FormattingOptions
			{
				UseTabs = true,
				IndentSize = 4
			}, 
			_ => new FormattingOptions(), 
		};
		AnsiConsole.WriteLine();
		AnsiConsole.MarkupLine("[bold]4. Fallback Scan Paths[/]");
		AnsiConsole.MarkupLine("[dim]Used when no solution file is found (Roslyn is primary)[/]");
		string scanPathChoice = AnsiConsole.Prompt(
			new SelectionPrompt<string>()
				.Title("Scan path configuration:")
				.AddChoices(
					"Default (Assets/**/*EntityAPI*.cs)",
					"All C# files (Assets/**/*.cs)",
					"Custom patterns"));
		List<string> scanPaths = ((scanPathChoice == "All C# files (Assets/**/*.cs)") ? new List<string> { "Assets/**/*.cs", "Packages/**/*.cs" } : ((!(scanPathChoice == "Custom patterns")) ? new List<string> { "Assets/**/*EntityAPI*.cs", "Packages/**/*EntityAPI*.cs" } : PromptForPatterns("scan")));
		codeGenConfig.ScanPaths = scanPaths;
		AnsiConsole.WriteLine();
		AnsiConsole.MarkupLine("[bold]5. Exclude Paths[/]");
		string excludePathChoice = AnsiConsole.Prompt(
			new SelectionPrompt<string>()
				.Title("Exclude path configuration:")
				.AddChoices(
					"Default (obj, Library, Temp, *.g.cs)",
					"Add custom exclusions",
					"Only custom patterns"));
		scanPaths = ((excludePathChoice == "Add custom exclusions") ? GetDefaultExcludePaths().Concat(PromptForPatterns("exclude")).ToList() : ((!(excludePathChoice == "Only custom patterns")) ? GetDefaultExcludePaths() : PromptForPatterns("exclude")));
		codeGenConfig.ExcludePaths = scanPaths;
		return Task.FromResult(codeGenConfig);
	}

	private static List<string> PromptForPatterns(string patternType)
	{
		List<string> patterns = new List<string>();
		AnsiConsole.MarkupLine("[dim]Enter " + patternType + " patterns (empty line to finish):[/]");
		while (true)
		{
			string pattern = AnsiConsole.Prompt(new TextPrompt<string>($"[dim]Pattern {patterns.Count + 1}:[/]").AllowEmpty());
			if (string.IsNullOrWhiteSpace(pattern))
			{
				break;
			}
			patterns.Add(pattern);
		}
		if (patterns.Count <= 0)
		{
			return GetDefaultScanPaths();
		}
		return patterns;
	}

	private static void ShowConfigPreview(CodeGenConfig config)
	{
		AnsiConsole.WriteLine();
		AnsiConsole.Write(new Rule("[bold]Configuration Preview[/]"));
		Table table = new Table().Border(TableBorder.Rounded).AddColumn("[bold]Setting[/]").AddColumn("[bold]Value[/]");
		table.AddRow("Project Root", Markup.Escape(config.ProjectRoot));
		table.AddRow("Verbose", config.Verbose ? "[green]Yes[/]" : "[dim]No[/]");
		table.AddRow("Track Orphans", config.TrackOrphans ? "[green]Yes[/]" : "[dim]No[/]");
		table.AddRow("Indentation", config.Formatting.UseTabs ? "Tabs" : $"Spaces ({config.Formatting.IndentSize})");
		AnsiConsole.Write(table);
		AnsiConsole.WriteLine();
		AnsiConsole.MarkupLine("[bold]Scan Paths (fallback):[/]");
		foreach (string scanPath in config.ScanPaths)
		{
			AnsiConsole.MarkupLine("  [blue]•[/] " + Markup.Escape(scanPath));
		}
		AnsiConsole.WriteLine();
		AnsiConsole.MarkupLine("[bold]Exclude Paths:[/]");
		foreach (string excludePath in config.ExcludePaths)
		{
			AnsiConsole.MarkupLine("  [red]•[/] " + Markup.Escape(excludePath));
		}
		AnsiConsole.WriteLine();
	}

	private static CodeGenConfig CreateDefaultConfig(string projectPath)
	{
		return new CodeGenConfig
		{
			ProjectRoot = projectPath,
			ScanPaths = GetDefaultScanPaths(),
			ExcludePaths = GetDefaultExcludePaths(),
			Verbose = false,
			TrackOrphans = true,
			Formatting = new FormattingOptions
			{
				UseTabs = false,
				IndentSize = 4,
				NewLine = Environment.NewLine
			}
		};
	}

	private static List<string> GetDefaultScanPaths()
	{
		return new List<string> { "Assets/**/*EntityAPI*.cs", "Packages/**/*EntityAPI*.cs" };
	}

	private static List<string> GetDefaultExcludePaths()
	{
		return new List<string> { "**/obj/**", "**/Library/**", "**/Temp/**", "**/*.g.cs", "**/*.generated.cs" };
	}
}
