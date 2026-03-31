using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Atomic.CodeGen.Core.Models;
using Atomic.CodeGen.Utils;
using Spectre.Console;

namespace Atomic.CodeGen.Commands;

public static class ConfigureCommand
{
	public static Command Create()
	{
		Option<string> option = new Option<string>(new string[2] { "--project", "-p" }, () => Directory.GetCurrentDirectory(), "Path to project root");
		Command command = new Command("configure", "View and modify configuration settings") { option };
		command.SetHandler(async delegate(string projectPath)
		{
			string configPath = Path.Combine(projectPath, "atomic-codegen.json");
			bool isNew = false;
			CodeGenConfig config;
			if (File.Exists(configPath))
			{
				config = await ConfigLoader.LoadAsync(projectPath);
			}
			else
			{
				AnsiConsole.MarkupLine("[yellow]No configuration file found. Creating new configuration...[/]");
				config = CreateDefaultConfig(projectPath);
				isNew = true;
			}
			bool hasChanges = isNew;
			while (true)
			{
				AnsiConsole.Clear();
				ShowCurrentConfig(config, configPath, hasChanges);
				string selectedOption = AnsiConsole.Prompt(
					new SelectionPrompt<string>()
						.Title("[bold]What would you like to do?[/]")
						.PageSize(16)
						.AddChoices(
							"Edit Analyzer Mode",
							"Edit Included Projects",
							"Edit Verbose Logging",
							"Edit Orphan Tracking",
							"Edit Include Timestamp",
							"Edit Backup Cap",
							"Edit Formatting",
							"Edit Scan Paths",
							"Edit Exclude Paths",
							"─────────────────",
							"Reset to Defaults",
							hasChanges ? "[green]Save Changes[/]" : "[dim]Save (no changes)[/]",
							"Exit without Saving"));
				if (selectedOption.Contains("Exit"))
				{
					if (!hasChanges || AnsiConsole.Confirm("[yellow]You have unsaved changes. Exit anyway?[/]", defaultValue: false))
					{
						break;
					}
				}
				else if (selectedOption.Contains("Save"))
				{
					if (hasChanges)
					{
						await ConfigLoader.SaveAsync(config, projectPath);
						AnsiConsole.MarkupLine("[green]✓ Configuration saved![/]");
						AnsiConsole.WriteLine();
						AnsiConsole.MarkupLine("Press any key to continue...");
						Console.ReadKey(intercept: true);
						hasChanges = false;
					}
				}
				else if (selectedOption.Contains("Reset"))
				{
					if (AnsiConsole.Confirm("Reset all settings to defaults?", defaultValue: false))
					{
						config = CreateDefaultConfig(projectPath);
						hasChanges = true;
						AnsiConsole.MarkupLine("[yellow]Settings reset to defaults[/]");
						await Task.Delay(1000);
					}
				}
				else if (selectedOption.Contains("Analyzer Mode"))
				{
					config.AnalyzerMode = EditAnalyzerMode(config.AnalyzerMode);
					hasChanges = true;
				}
				else if (selectedOption.Contains("Included Projects"))
				{
					List<string> updatedProjects = await EditIncludedProjectsAsync(projectPath, config.IncludedProjects);
					if (updatedProjects != null)
					{
						config.IncludedProjects = ((updatedProjects.Count > 0) ? updatedProjects : null);
						hasChanges = true;
					}
				}
				else if (selectedOption.Contains("Verbose"))
				{
					config.Verbose = EditBoolSetting("Verbose Logging", config.Verbose, "Show detailed output during code generation");
					hasChanges = true;
				}
				else if (selectedOption.Contains("Orphan"))
				{
					config.TrackOrphans = EditBoolSetting("Orphan Tracking", config.TrackOrphans, "Automatically clean up generated files when source is deleted");
					hasChanges = true;
				}
				else if (selectedOption.Contains("Timestamp"))
				{
					config.IncludeTimestamp = EditBoolSetting("Include Timestamp", config.IncludeTimestamp, "Add 'Generated at' timestamp to file headers (disable for cleaner git diffs)");
					hasChanges = true;
				}
				else if (selectedOption.Contains("Backup Cap"))
				{
					config.BackupCap = EditBackupCap(config.BackupCap);
					hasChanges = true;
				}
				else if (selectedOption.Contains("Formatting"))
				{
					config.Formatting = EditFormatting(config.Formatting);
					hasChanges = true;
				}
				else if (selectedOption.Contains("Scan Paths"))
				{
					config.ScanPaths = EditPathList("Scan Paths", config.ScanPaths, "Glob patterns for finding EntityAPI files (fallback when no .sln)");
					hasChanges = true;
				}
				else if (selectedOption.Contains("Exclude Paths"))
				{
					config.ExcludePaths = EditPathList("Exclude Paths", config.ExcludePaths, "Glob patterns for excluding files from scanning");
					hasChanges = true;
				}
			}
		}, option);
		return command;
	}

	private static void ShowCurrentConfig(CodeGenConfig config, string configPath, bool hasChanges)
	{
		AnsiConsole.Write(new FigletText("Configure").LeftJustified().Color(Color.Blue));
		AnsiConsole.Write(new Rule(hasChanges ? "[bold blue]Current Configuration[/] [yellow](unsaved changes)[/]" : "[bold blue]Current Configuration[/]"));
		AnsiConsole.MarkupLine("[dim]File: " + Markup.Escape(configPath) + "[/]");
		AnsiConsole.WriteLine();
		Table table = new Table().Border(TableBorder.Rounded).AddColumn("[bold]Setting[/]").AddColumn("[bold]Value[/]");
		string analyzerModeDisplay = config.AnalyzerMode switch
		{
			AnalyzerMode.Auto => "[cyan]Auto[/] [dim](MSBuild → Buildalyzer)[/]",
			AnalyzerMode.MSBuild => "[yellow]MSBuild[/] [dim](requires VS/SDK)[/]",
			AnalyzerMode.Buildalyzer => "[green]Buildalyzer[/] [dim](no VS/SDK needed)[/]",
			_ => config.AnalyzerMode.ToString(),
		};
		table.AddRow("Analyzer Mode", analyzerModeDisplay);
		string projectsDisplay = ((config.IncludedProjects == null || config.IncludedProjects.Count == 0) ? "[cyan]All projects[/]" : $"[yellow]{config.IncludedProjects.Count}[/] selected");
		table.AddRow("Included Projects", projectsDisplay);
		table.AddRow("Verbose Logging", config.Verbose ? "[green]Enabled[/]" : "[dim]Disabled[/]");
		table.AddRow("Orphan Tracking", config.TrackOrphans ? "[green]Enabled[/]" : "[dim]Disabled[/]");
		table.AddRow("Include Timestamp", config.IncludeTimestamp ? "[green]Enabled[/]" : "[dim]Disabled[/]");
		table.AddRow("Backup Cap", (config.BackupCap == 0) ? "[yellow]Unlimited[/]" : $"[cyan]{config.BackupCap}[/] backups");
		table.AddRow("Indentation", config.Formatting.UseTabs ? "Tabs" : $"Spaces ({config.Formatting.IndentSize})");
		AnsiConsole.Write(table);
		AnsiConsole.WriteLine();
		AnsiConsole.Write(new Panel(new Rows(config.ScanPaths.Select((string p) => new Markup("[blue]•[/] " + Markup.Escape(p)))))
		{
			Header = new PanelHeader("[bold]Scan Paths (fallback)[/]"),
			Border = BoxBorder.Rounded
		});
		AnsiConsole.Write(new Panel(new Rows(config.ExcludePaths.Select((string p) => new Markup("[red]•[/] " + Markup.Escape(p)))))
		{
			Header = new PanelHeader("[bold]Exclude Paths[/]"),
			Border = BoxBorder.Rounded
		});
		AnsiConsole.WriteLine();
	}

	private static bool EditBoolSetting(string name, bool currentValue, string description)
	{
		AnsiConsole.Clear();
		AnsiConsole.Write(new Rule("[bold]Edit " + name + "[/]"));
		AnsiConsole.MarkupLine("[dim]" + description + "[/]");
		AnsiConsole.WriteLine();
		AnsiConsole.MarkupLine("Current value: " + (currentValue ? "[green]Enabled[/]" : "[dim]Disabled[/]"));
		AnsiConsole.WriteLine();
		return AnsiConsole.Prompt(new SelectionPrompt<string>().Title("New value:").AddChoices("Enable", "Disable")) == "Enable";
	}

	private static async Task<List<string>?> EditIncludedProjectsAsync(string projectPath, List<string>? currentProjects)
	{
		AnsiConsole.Clear();
		AnsiConsole.Write(new Rule("[bold]Edit Included Projects[/]"));
		AnsiConsole.MarkupLine("[dim]Select which projects to include in analysis.[/]");
		AnsiConsole.MarkupLine("[dim]Fewer projects = faster analysis (especially with Buildalyzer).[/]");
		AnsiConsole.WriteLine();
		string[] files = Directory.GetFiles(projectPath, "*.sln", SearchOption.TopDirectoryOnly);
		if (files.Length == 0)
		{
			AnsiConsole.MarkupLine("[yellow]No solution file found in project directory.[/]");
			AnsiConsole.MarkupLine("[dim]Project filtering requires a .sln file.[/]");
			AnsiConsole.WriteLine();
			AnsiConsole.MarkupLine("Press any key to continue...");
			Console.ReadKey(intercept: true);
			return null;
		}
		string solutionPath = files[0];
		AnsiConsole.MarkupLine("[dim]Solution: " + Path.GetFileName(solutionPath) + "[/]");
		AnsiConsole.WriteLine();
		List<string> solutionProjects;
		try
		{
			solutionProjects = await AnsiConsole.Status().Spinner(Spinner.Known.Dots).StartAsync("Scanning solution for projects...", async (StatusContext ctx) => await Task.Run(() => GetProjectsFromSolution(solutionPath)));
		}
		catch (Exception ex)
		{
			AnsiConsole.MarkupLine("[red]Error scanning solution: " + ex.Message + "[/]");
			AnsiConsole.WriteLine();
			AnsiConsole.MarkupLine("Press any key to continue...");
			Console.ReadKey(intercept: true);
			return null;
		}
		if (solutionProjects.Count == 0)
		{
			AnsiConsole.MarkupLine("[yellow]No projects found in solution.[/]");
			AnsiConsole.WriteLine();
			AnsiConsole.MarkupLine("Press any key to continue...");
			Console.ReadKey(intercept: true);
			return null;
		}
		AnsiConsole.MarkupLine($"[green]Found {solutionProjects.Count} projects[/]");
		AnsiConsole.WriteLine();
		HashSet<string> currentProjectSet = ((currentProjects != null) ? new HashSet<string>(currentProjects, StringComparer.OrdinalIgnoreCase) : null);
		bool selectAll = currentProjectSet == null || currentProjectSet.Count == 0;
		MultiSelectionPrompt<string> multiSelectionPrompt = new MultiSelectionPrompt<string>()
			.Title("[bold]Select projects to include:[/]")
			.PageSize(15)
			.InstructionsText("[dim](Press [blue]<space>[/] to toggle, [green]<enter>[/] to confirm)[/]")
			.AddChoices(solutionProjects);
		if (selectAll)
		{
			foreach (string project in solutionProjects)
			{
				multiSelectionPrompt.Select(project);
			}
		}
		else
		{
			foreach (string project in solutionProjects)
			{
				if (currentProjectSet.Contains(project))
				{
					multiSelectionPrompt.Select(project);
				}
			}
		}
		List<string> selectedProjects = AnsiConsole.Prompt(multiSelectionPrompt);
		if (selectedProjects.Count == solutionProjects.Count)
		{
			AnsiConsole.MarkupLine("[cyan]All projects selected - will use all projects[/]");
			Thread.Sleep(1000);
			return new List<string>();
		}
		if (selectedProjects.Count == 0)
		{
			AnsiConsole.MarkupLine("[yellow]No projects selected - will use all projects[/]");
			Thread.Sleep(1000);
			return new List<string>();
		}
		AnsiConsole.MarkupLine($"[green]✓ Selected {selectedProjects.Count}/{solutionProjects.Count} projects[/]");
		Thread.Sleep(1000);
		return selectedProjects;
	}

	private static List<string> GetProjectsFromSolution(string solutionPath)
	{
		List<string> projectPaths = new List<string>();
		string[] solutionLines = File.ReadAllLines(solutionPath);
		foreach (string line in solutionLines)
		{
			if (!line.StartsWith("Project(") || !line.Contains(".csproj"))
			{
				continue;
			}
			string[] segments = line.Split('"');
			if (segments.Length >= 4)
			{
				string relativePath = segments[3];
				if (!string.IsNullOrWhiteSpace(relativePath))
				{
					projectPaths.Add(relativePath);
				}
			}
		}
		return projectPaths.OrderBy((string p) => p).ToList();
	}

	private static AnalyzerMode EditAnalyzerMode(AnalyzerMode currentValue)
	{
		AnsiConsole.Clear();
		AnsiConsole.Write(new Rule("[bold]Edit Analyzer Mode[/]"));
		AnsiConsole.MarkupLine("[dim]Controls which analyzer to use for semantic analysis.[/]");
		AnsiConsole.WriteLine();
		AnsiConsole.MarkupLine("Current: " + currentValue switch
		{
			AnalyzerMode.Auto => "[cyan]Auto[/]", 
			AnalyzerMode.MSBuild => "[yellow]MSBuild[/]", 
			AnalyzerMode.Buildalyzer => "[green]Buildalyzer[/]", 
			_ => currentValue.ToString(), 
		});
		AnsiConsole.WriteLine();
		AnsiConsole.Write(new Panel(new Markup("[bold cyan]Auto[/] [dim](recommended)[/]\n  Tries MSBuild first, falls back to Buildalyzer\n\n[bold yellow]MSBuild[/]\n  Force MSBuild only (faster, requires VS or .NET SDK)\n\n[bold green]Buildalyzer[/]\n  Force Buildalyzer only (no VS/SDK required, may be slower)"))
		{
			Header = new PanelHeader("[bold]Options[/]"),
			Border = BoxBorder.Rounded,
			Padding = new Padding(2, 1)
		});
		AnsiConsole.WriteLine();
		return AnsiConsole.Prompt(
			new SelectionPrompt<string>()
				.Title("Select analyzer mode:")
				.AddChoices(
					"Auto (recommended)",
					"MSBuild (requires VS/SDK)",
					"Buildalyzer (no VS/SDK needed)")) switch
		{
			"Auto (recommended)" => AnalyzerMode.Auto, 
			"MSBuild (requires VS/SDK)" => AnalyzerMode.MSBuild, 
			"Buildalyzer (no VS/SDK needed)" => AnalyzerMode.Buildalyzer, 
			_ => currentValue, 
		};
	}

	private static int EditBackupCap(int currentValue)
	{
		AnsiConsole.Clear();
		AnsiConsole.Write(new Rule("[bold]Edit Backup Cap[/]"));
		AnsiConsole.MarkupLine("[dim]Maximum number of rename backups to keep. Set to 0 for unlimited.[/]");
		AnsiConsole.MarkupLine("[dim]Oldest backups are deleted when the cap is reached.[/]");
		AnsiConsole.WriteLine();
		AnsiConsole.MarkupLine("Current value: [cyan]" + ((currentValue == 0) ? "Unlimited" : currentValue.ToString()) + "[/]");
		AnsiConsole.WriteLine();
		return AnsiConsole.Prompt(
			new SelectionPrompt<string>()
				.Title("Select backup cap:")
				.AddChoices(
					"5 backups",
					"10 backups",
					"20 backups",
					"50 backups",
					"Unlimited (0)",
					"Custom...")) switch
		{
			"Custom..." => AnsiConsole.Prompt(
				new TextPrompt<int>("Enter backup cap (0 for unlimited):")
					.DefaultValue(currentValue)
					.Validate((int n) => (n < 0)
						? ValidationResult.Error("Must be 0 or greater")
						: ValidationResult.Success())),
			"5 backups" => 5, 
			"10 backups" => 10, 
			"20 backups" => 20, 
			"50 backups" => 50, 
			"Unlimited (0)" => 0, 
			_ => currentValue, 
		};
	}

	private static FormattingOptions EditFormatting(FormattingOptions current)
	{
		AnsiConsole.Clear();
		AnsiConsole.Write(new Rule("[bold]Edit Formatting[/]"));
		AnsiConsole.MarkupLine("[dim]Configure code formatting for generated files[/]");
		AnsiConsole.WriteLine();
		string currentDisplay = (current.UseTabs ? "Tabs" : $"Spaces ({current.IndentSize})");
		AnsiConsole.MarkupLine("Current: [yellow]" + currentDisplay + "[/]");
		AnsiConsole.WriteLine();
		return AnsiConsole.Prompt(
			new SelectionPrompt<string>()
				.Title("Indentation style:")
				.AddChoices("Spaces (4)", "Spaces (2)", "Tabs")) switch
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
			_ => current, 
		};
	}

	private static List<string> EditPathList(string name, List<string> currentPaths, string description)
	{
		List<string> patterns = currentPaths.ToList();
		while (true)
		{
			AnsiConsole.Clear();
			AnsiConsole.Write(new Rule("[bold]Edit " + name + "[/]"));
			AnsiConsole.MarkupLine("[dim]" + description + "[/]");
			AnsiConsole.WriteLine();
			AnsiConsole.MarkupLine("[bold]Current patterns:[/]");
			for (int i = 0; i < patterns.Count; i++)
			{
				AnsiConsole.MarkupLine($"  [yellow]{i + 1}.[/] {Markup.Escape(patterns[i])}");
			}
			if (patterns.Count == 0)
			{
				AnsiConsole.MarkupLine("  [dim](none)[/]");
			}
			AnsiConsole.WriteLine();
			List<string> choices = new List<string> { "Add Pattern", "Remove Pattern", "Clear All", "Done" };
			switch (AnsiConsole.Prompt(new SelectionPrompt<string>().Title("Action:").AddChoices(choices)))
			{
			case "Add Pattern":
			{
				string newPattern = AnsiConsole.Prompt(
					new TextPrompt<string>("Enter pattern (e.g., [blue]Assets/**/*.cs[/]):")
						.Validate((string p) => string.IsNullOrWhiteSpace(p)
							? ValidationResult.Error("Pattern cannot be empty")
							: ValidationResult.Success()));
				patterns.Add(newPattern);
				AnsiConsole.MarkupLine("[green]✓ Added:[/] " + Markup.Escape(newPattern));
				Thread.Sleep(500);
				break;
			}
			case "Remove Pattern":
			{
				if (patterns.Count == 0)
				{
					AnsiConsole.MarkupLine("[yellow]No patterns to remove[/]");
					Thread.Sleep(1000);
					break;
				}
				string patternToRemove = AnsiConsole.Prompt(new SelectionPrompt<string>().Title("Select pattern to remove:").AddChoices(patterns.Concat(new string[1] { "[Cancel]" })));
				if (patternToRemove != "[Cancel]")
				{
					patterns.Remove(patternToRemove);
					AnsiConsole.MarkupLine("[red]✓ Removed:[/] " + Markup.Escape(patternToRemove));
					Thread.Sleep(500);
				}
				break;
			}
			case "Clear All":
				if (AnsiConsole.Confirm("Remove all patterns?", defaultValue: false))
				{
					patterns.Clear();
					AnsiConsole.MarkupLine("[yellow]All patterns removed[/]");
					Thread.Sleep(500);
				}
				break;
			case "Done":
				return patterns;
			}
		}
	}

	private static CodeGenConfig CreateDefaultConfig(string projectPath)
	{
		return new CodeGenConfig
		{
			ProjectRoot = projectPath,
			ScanPaths = new List<string> { "Assets/**/*EntityAPI*.cs", "Packages/**/*EntityAPI*.cs" },
			ExcludePaths = new List<string> { "**/obj/**", "**/Library/**", "**/Temp/**", "**/*.g.cs", "**/*.generated.cs" },
			Verbose = false,
			TrackOrphans = true,
			BackupCap = 10,
			Formatting = new FormattingOptions
			{
				UseTabs = false,
				IndentSize = 4,
				NewLine = Environment.NewLine
			}
		};
	}
}
