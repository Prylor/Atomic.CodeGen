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

public static class WizardCommand
{
	public static Command Create()
	{
		Option<string> projectOption = new Option<string>(["--project", "-p"], () => Directory.GetCurrentDirectory(), "Path to project root");
		Command command = new Command("wizard", "Interactive setup wizard - complete onboarding experience") { projectOption };
		command.SetHandler(async (string projectPath) =>
		{
			bool? frameworkCheckResult = await CheckAtomicFrameworkAsync(projectPath);
			if (frameworkCheckResult != false && (!frameworkCheckResult.HasValue || (ShowWelcome() && ShowFeatureEntityApi() && ShowFeatureBehaviours() && ShowFeatureEntityDomain() && ShowFeatureSmartRename())) && await ShowConfigurationWizard(projectPath) != null)
			{
				ShowIdeSetup();
				ShowComplete(projectPath);
			}
		}, projectOption);
		return command;
	}

	private static async Task<bool?> CheckAtomicFrameworkAsync(string projectPath)
	{
		AnsiConsole.Clear();
		AnsiConsole.Write(new FigletText("Atomic CodeGen").LeftJustified().Color(Color.Blue));
		AnsiConsole.Write(new Rule("[bold blue]Checking Atomic Framework[/]"));
		AnsiConsole.WriteLine();
		string solutionPath = FindSolutionFile(projectPath);
		if (solutionPath == null)
		{
			AnsiConsole.MarkupLine("[yellow]No .sln file found in project directory.[/]");
			AnsiConsole.MarkupLine("[dim]Framework check skipped - will verify during code generation.[/]");
			AnsiConsole.WriteLine();
			string selectedAction = AnsiConsole.Prompt(
				new SelectionPrompt<string>()
					.Title("[bold]What would you like to do?[/]")
					.AddChoices(
						"Continue with wizard",
						"Skip to configuration",
						"Exit wizard"));
			if (selectedAction.Contains("Exit"))
			{
				return false;
			}
			if (selectedAction.Contains("Skip"))
			{
				return null;
			}
			return true;
		}
		AnsiConsole.MarkupLine("[dim]Scanning solution for Atomic Framework...[/]");
		AnsiConsole.WriteLine();
		bool hasEntityApiAttribute = false;
		bool hasLinkToAttribute = false;
		bool hasEntityDomainBuilder = false;
		try
		{
			await AnsiConsole.Status().Spinner(Spinner.Known.Dots).StartAsync("Analyzing solution...", async (_) =>
			{
				using SemanticTypeDiscovery discovery = new SemanticTypeDiscovery(solutionPath);
				(hasEntityApiAttribute, hasLinkToAttribute, hasEntityDomainBuilder) = await discovery.CheckAtomicFrameworkAsync();
			});
		}
		catch (Exception ex)
		{
			AnsiConsole.MarkupLine("[yellow]Warning: Could not analyze solution: " + Markup.Escape(ex.Message) + "[/]");
			AnsiConsole.MarkupLine("[dim]Framework check skipped.[/]");
			AnsiConsole.WriteLine();
			string errorAction = AnsiConsole.Prompt(
				new SelectionPrompt<string>()
					.Title("[bold]What would you like to do?[/]")
					.AddChoices(
						"Continue with wizard",
						"Skip to configuration",
						"Exit wizard"));
			if (errorAction.Contains("Exit"))
			{
				return false;
			}
			if (errorAction.Contains("Skip"))
			{
				return null;
			}
			return true;
		}
		if (hasEntityApiAttribute && hasLinkToAttribute && hasEntityDomainBuilder)
		{
			AnsiConsole.MarkupLine("[green]Atomic Framework detected![/]");
			AnsiConsole.MarkupLine("  [green][[EntityAPI]][/] attribute found");
			AnsiConsole.MarkupLine("  [green][[LinkTo]][/] attribute found");
			AnsiConsole.MarkupLine("  [green]EntityDomainBuilder[/] found");
			AnsiConsole.WriteLine();
			string frameworkFoundAction = AnsiConsole.Prompt(
				new SelectionPrompt<string>()
					.Title("[bold]What would you like to do?[/]")
					.AddChoices(
						"Continue with wizard",
						"Skip to configuration",
						"Exit wizard"));
			if (frameworkFoundAction.Contains("Exit"))
			{
				return false;
			}
			if (frameworkFoundAction.Contains("Skip"))
			{
				return null;
			}
			return true;
		}
		AnsiConsole.Clear();
		AnsiConsole.Write(new FigletText("Missing!").LeftJustified().Color(Color.Red));
		AnsiConsole.Write(new Rule("[bold red]Atomic Framework Not Found[/]"));
		AnsiConsole.WriteLine();
		List<string> missingComponents = new List<string>();
		if (!hasEntityApiAttribute)
		{
			missingComponents.Add("[[EntityAPI]] attribute");
		}
		if (!hasLinkToAttribute)
		{
			missingComponents.Add("[[LinkTo]] attribute");
		}
		if (!hasEntityDomainBuilder)
		{
			missingComponents.Add("EntityDomainBuilder class");
		}
		AnsiConsole.Write(new Panel(new Markup("[bold red]Atomic Framework is not installed in your project![/]\n\n[bold]Missing components:[/]\n" + string.Join("\n", missingComponents.Select((string m) => "  [red]x[/] " + m)) + "\n\n[bold]To use Atomic CodeGen, you need to install the Atomic Framework first.[/]\n\n[bold yellow]Installation:[/]\n\n  1. Visit: [cyan underline]https://github.com/StarKRE22/Atomic[/]\n  2. Follow the installation instructions in the README\n  3. Import the package into your project\n  4. Run this wizard again"))
		{
			Header = new PanelHeader("[bold red]Framework Required[/]"),
			Border = BoxBorder.Double,
			Padding = new Padding(2, 1)
		});
		AnsiConsole.WriteLine();
		string missingAction = AnsiConsole.Prompt(
			new SelectionPrompt<string>()
				.Title("[bold]What would you like to do?[/]")
				.AddChoices(
					"Continue anyway (for exploration)",
					"Skip to configuration",
					"Exit wizard"));
		if (missingAction.Contains("Exit"))
		{
			return false;
		}
		if (missingAction.Contains("Skip"))
		{
			return null;
		}
		return true;
	}

	private static string? FindSolutionFile(string projectPath)
	{
		return Directory.GetFiles(projectPath, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();
	}

	private static bool ShowWelcome()
	{
		AnsiConsole.Clear();
		AnsiConsole.Write(new FigletText("Atomic CodeGen").LeftJustified().Color(Color.Blue));
		AnsiConsole.Write(new Rule("[bold blue]Welcome to Setup Wizard[/]"));
		AnsiConsole.WriteLine();
		AnsiConsole.Write(new Panel(new Markup("[bold]Atomic CodeGen[/] is a powerful code generation tool\nfor the [cyan]Atomic Entity Framework[/].\n\n[dim]This wizard will guide you through:[/]\n\n  [green]1.[/] Understanding what the tool does\n  [green]2.[/] Configuring your project\n  [green]3.[/] Setting up IDE integration\n  [green]4.[/] Getting started with code generation\n\n[dim]You can exit at any time by selecting 'Exit'[/]"))
		{
			Header = new PanelHeader("[bold yellow]Welcome![/]"),
			Border = BoxBorder.Double,
			Padding = new Padding(2, 1)
		});
		AnsiConsole.WriteLine();
		return PromptNavigation("Let's get started!");
	}

	private static bool ShowFeatureEntityApi()
	{
		AnsiConsole.Clear();
		AnsiConsole.Write(new FigletText("Entity API").LeftJustified().Color(Color.Green));
		AnsiConsole.Write(new Rule("[bold green]Feature 1/4: Entity API Generation[/]"));
		AnsiConsole.WriteLine();
		AnsiConsole.Write(new Panel(new Markup("[bold]Define your entity structure using attributes:[/]\n\n[dim][[EntityAPI]][/]\n[dim]public static partial class CharacterAPI[/]\n[dim]{[/]\n[dim]    enum Tags { Player, Enemy, Dead }[/]\n[dim]    class Values { int Health; float Speed; }[/]\n[dim]}[/]\n\n[bold]Tool generates extension methods:[/]\n\n[green]entity.GetHealth()[/]     [dim]// Get value[/]\n[green]entity.SetSpeed(5f)[/]    [dim]// Set value[/]\n[green]entity.IsPlayer()[/]      [dim]// Check tag[/]\n[green]entity.AddDead()[/]       [dim]// Add tag[/]"))
		{
			Header = new PanelHeader("[bold cyan]Entity API[/]"),
			Border = BoxBorder.Double,
			Padding = new Padding(2, 1)
		});
		return PromptNavigation("Next: Behaviours");
	}

	private static bool ShowFeatureBehaviours()
	{
		AnsiConsole.Clear();
		AnsiConsole.Write(new FigletText("Behaviours").LeftJustified().Color(Color.Green));
		AnsiConsole.Write(new Rule("[bold green]Feature 2/4: Behaviour Linking[/]"));
		AnsiConsole.WriteLine();
		AnsiConsole.Write(new Panel(new Markup("[bold]Link behaviour classes to an EntityAPI:[/]\n\n[dim][[LinkTo(typeof(CharacterAPI))]][/]\n[dim]public class JumpBehaviour : IEntityInit, IEntityTick[/]\n[dim]{[/]\n[dim]    public void Init(IEntity entity) { }[/]\n[dim]    public void Tick(IEntity entity, float dt) { }[/]\n[dim]}[/]\n\n[bold]Tool generates behaviour extension methods:[/]\n\n[green]entity.AddJumpBehaviour()[/]   [dim]// Add behaviour[/]\n[green]entity.GetJumpBehaviour()[/]   [dim]// Get behaviour[/]\n[green]entity.HasJumpBehaviour()[/]   [dim]// Check if has[/]"))
		{
			Header = new PanelHeader("[bold cyan]Behaviours with [[LinkTo]][/]"),
			Border = BoxBorder.Double,
			Padding = new Padding(2, 1)
		});
		return PromptNavigation("Next: Entity Domain");
	}

	private static bool ShowFeatureEntityDomain()
	{
		AnsiConsole.Clear();
		AnsiConsole.Write(new FigletText("Domain").LeftJustified().Color(Color.Green));
		AnsiConsole.Write(new Rule("[bold green]Feature 3/4: Entity Domain Generation[/]"));
		AnsiConsole.WriteLine();
		AnsiConsole.Write(new Panel(new Markup("[bold]Generate complete entity infrastructure:[/]\n\n  [yellow]Interfaces[/]  - ICharacter, ICharacterSingleton\n  [yellow]Installers[/]  - Scriptable, Scene-based\n  [yellow]Factories[/]   - Create entities from configs\n  [yellow]Pools[/]       - Object pooling support\n  [yellow]Views[/]       - UI binding helpers\n  [yellow]Bakers[/]      - Convert data to entities\n\n[dim]Configure what to generate using EntityDomainBuilder[/]"))
		{
			Header = new PanelHeader("[bold cyan]Entity Domain[/]"),
			Border = BoxBorder.Double,
			Padding = new Padding(2, 1)
		});
		return PromptNavigation("Next: Smart Rename");
	}

	private static bool ShowFeatureSmartRename()
	{
		AnsiConsole.Clear();
		AnsiConsole.Write(new FigletText("Rename").LeftJustified().Color(Color.Green));
		AnsiConsole.Write(new Rule("[bold green]Feature 4/4: Smart Rename[/]"));
		AnsiConsole.WriteLine();
		AnsiConsole.Write(new Panel(new Markup("[bold]Rename Tags, Values, or Behaviours safely:[/]\n\n  [green]1.[/] Finds all usages across entire codebase\n  [green]2.[/] Renames in source definitions\n  [green]3.[/] Renames in all usage sites\n  [green]4.[/] Regenerates affected API files\n\n[bold]Usage:[/]\n[cyan]atomic-codegen rename[/]        [dim]Interactive mode[/]\n[cyan]atomic-codegen rename-at[/]     [dim]Rename at cursor[/]"))
		{
			Header = new PanelHeader("[bold cyan]Smart Rename[/]"),
			Border = BoxBorder.Double,
			Padding = new Padding(2, 1)
		});
		return PromptNavigation("Continue to configuration");
	}

	private static async Task<CodeGenConfig?> ShowConfigurationWizard(string projectPath)
	{
		AnsiConsole.Clear();
		AnsiConsole.Write(new FigletText("Configure").LeftJustified().Color(Color.Yellow));
		AnsiConsole.Write(new Rule("[bold yellow]Project Configuration[/]"));
		AnsiConsole.WriteLine();
		string configPath = Path.Combine(projectPath, "atomic-codegen.json");
		if (File.Exists(configPath))
		{
			AnsiConsole.MarkupLine("[yellow]Configuration file found:[/] " + Markup.Escape(configPath));
			AnsiConsole.WriteLine();
			string configAction = AnsiConsole.Prompt(
				new SelectionPrompt<string>()
					.Title("[bold]What would you like to do?[/]")
					.AddChoices(
						"Use existing configuration",
						"Create new configuration",
						"Exit wizard"));
			if (configAction.Contains("Exit"))
			{
				return null;
			}
			if (configAction.Contains("existing"))
			{
				return await ConfigLoader.LoadAsync(projectPath);
			}
		}
		CodeGenConfig config = new CodeGenConfig
		{
			ProjectRoot = projectPath
		};
		AnsiConsole.WriteLine();
		ShowParamDescription("Verbose Logging", "When enabled, the tool outputs detailed information during code generation.\nUseful for debugging issues or understanding what the tool is doing.\n[dim]Recommended: Off for normal use, On when troubleshooting[/]");
		config.Verbose = AnsiConsole.Confirm("Enable verbose logging?", defaultValue: false);
		AnsiConsole.WriteLine();
		ShowParamDescription("Orphan Tracking", "Automatically detects and removes generated files when their source is deleted.\nKeeps your project clean by removing stale generated code.\n[dim]Recommended: On[/]");
		config.TrackOrphans = AnsiConsole.Confirm("Enable orphan file tracking?");
		AnsiConsole.WriteLine();
		ShowParamDescription("Code Formatting", "Controls how generated code is indented.\nChoose based on your project's code style preferences.\n[dim]Most projects use Spaces (4)[/]");
		config.Formatting = AnsiConsole.Prompt(
			new SelectionPrompt<string>()
				.Title("Select indentation style:")
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
			_ => new FormattingOptions(), 
		};
		AnsiConsole.WriteLine();
		ShowParamDescription("Backup Cap", "Maximum number of rename backups to keep.\nWhen the limit is reached, oldest backups are deleted automatically.\n[dim]Recommended: 10 (set to 0 for unlimited)[/]");
		config.BackupCap = AnsiConsole.Prompt(
			new SelectionPrompt<string>()
				.Title("Select backup cap:")
				.AddChoices(
					"5 backups",
					"10 backups (recommended)",
					"20 backups",
					"Unlimited (0)")) switch
		{
			"5 backups" => 5, 
			"10 backups (recommended)" => 10, 
			"20 backups" => 20, 
			"Unlimited (0)" => 0, 
			_ => 10, 
		};
		AnsiConsole.WriteLine();
		ShowParamDescription("Exclude Paths", "Folders and patterns to exclude from scanning.\nDefault excludes: obj, Library, Temp, and generated files.\n[dim]You can add custom exclusions if needed[/]");
		string excludePathChoice = AnsiConsole.Prompt(
			new SelectionPrompt<string>()
				.Title("Exclude paths configuration:")
				.AddChoices("Use defaults (recommended)", "Add custom exclusions"));
		config.ExcludePaths = GetDefaultExcludePaths();
		if (excludePathChoice.Contains("custom"))
		{
			AnsiConsole.MarkupLine("[dim]Enter additional paths to exclude (empty to finish):[/]");
			while (true)
			{
				string excludePattern = AnsiConsole.Prompt(new TextPrompt<string>("[dim]Pattern:[/]").AllowEmpty());
				if (string.IsNullOrWhiteSpace(excludePattern))
				{
					break;
				}
				config.ExcludePaths.Add(excludePattern);
			}
		}
		AnsiConsole.WriteLine();
		AnsiConsole.Write(new Rule("[bold]Configuration Summary[/]"));
		Table table = new Table().Border(TableBorder.Rounded).AddColumn("[bold]Setting[/]").AddColumn("[bold]Value[/]");
		table.AddRow("Project Root", Markup.Escape(config.ProjectRoot));
		table.AddRow("Verbose", config.Verbose ? "[green]Yes[/]" : "[dim]No[/]");
		table.AddRow("Track Orphans", config.TrackOrphans ? "[green]Yes[/]" : "[dim]No[/]");
		table.AddRow("Backup Cap", (config.BackupCap == 0) ? "[yellow]Unlimited[/]" : $"{config.BackupCap} backups");
		table.AddRow("Indentation", config.Formatting.UseTabs ? "Tabs" : $"Spaces ({config.Formatting.IndentSize})");
		table.AddRow("Exclude Paths", $"{config.ExcludePaths.Count} patterns");
		AnsiConsole.Write(table);
		AnsiConsole.WriteLine();
		if (AnsiConsole.Confirm("Save this configuration?"))
		{
			await ConfigLoader.SaveAsync(config, projectPath);
			AnsiConsole.MarkupLine("[green]✓ Configuration saved to:[/] " + Markup.Escape(configPath));
			if (GitIgnoreHelper.EnsureRenameBackupIgnored(projectPath))
			{
				AnsiConsole.MarkupLine("[green]✓ Added .rename-backup/ to .gitignore[/]");
			}
		}
		AnsiConsole.WriteLine();
		AnsiConsole.MarkupLine("[dim]Press any key to continue...[/]");
		Console.ReadKey(intercept: true);
		return config;
	}

	private static void ShowIdeSetup()
	{
		AnsiConsole.Clear();
		AnsiConsole.Write(new FigletText("IDE Setup").LeftJustified().Color(Color.Purple));
		AnsiConsole.Write(new Rule("[bold purple]IDE Integration[/]"));
		AnsiConsole.WriteLine();
		if (!AnsiConsole.Prompt(
			new SelectionPrompt<string>()
				.Title("[bold]Would you like to set up IDE integration?[/]")
				.AddChoices("Yes, set up JetBrains Rider", "Skip IDE setup")).Contains("Skip"))
		{
			ShowRiderSetupWizard();
		}
	}

	private static void ShowRiderSetupWizard()
	{
		ShowRiderLiveTemplates();
		ShowRiderExternalTools();
	}

	private static void ShowRiderLiveTemplates()
	{
		ShowLiveTemplateSetup("eapi", "Entity API definition", GetEntityApiTemplate(), new(string, string, string)[3]
		{
			("$NAMESPACE$", "fileDefaultNamespace()", "Current file namespace"),
			("$CLASS_NAME$", "fileNameWithoutExtension()", "File name as class name"),
			("$END$", "(cursor position)", "Final cursor position")
		});
		ShowLiveTemplateSetup("edom", "Entity Domain definition", GetEntityDomainTemplate(), new(string, string, string)[4]
		{
			("$NAME$", "fileNameWithoutExtension()", "Entity name"),
			("$NAMESPACE$", "fileDefaultNamespace()", "Current namespace"),
			("$DIRECTORY$", "\"Assets/Scripts/Generated/\" + NAME", "Output directory"),
			("$END$", "(cursor position)", "Final cursor position")
		});
	}

	private static void ShowLiveTemplateSetup(string abbreviation, string description, string template, (string Variable, string Expression, string Description)[] variables)
	{
		AnsiConsole.Clear();
		AnsiConsole.Write(new Rule("[bold purple]Live Template: " + abbreviation + "[/]"));
		AnsiConsole.WriteLine();
		AnsiConsole.Write(new Panel(new Markup("[bold]Setting up '" + abbreviation + "' template:[/]\n\n1. Open Rider [yellow]Settings[/] ([dim]Ctrl+Alt+S[/])\n2. Go to [yellow]Editor -> Live Templates[/]\n3. Select [yellow]C#[/] group\n4. Click [green]+[/] -> [green]Live Template[/]\n5. Set Abbreviation: [bold green]" + abbreviation + "[/]\n6. Set Description: [dim]" + description + "[/]\n7. Copy and paste the template below\n8. Click [yellow]Edit variables[/] and configure as shown\n9. Set [yellow]Applicable in[/]: C# -> Everywhere\n10. Click [green]OK[/] to save"))
		{
			Header = new PanelHeader("[bold yellow]Step-by-Step Instructions[/]"),
			Border = BoxBorder.Rounded,
			Padding = new Padding(2, 1)
		});
		AnsiConsole.WriteLine();
		Table table = new Table().Border(TableBorder.Rounded).AddColumn("[bold]Variable[/]").AddColumn("[bold]Expression[/]")
			.AddColumn("[bold]Description[/]");
		for (int i = 0; i < variables.Length; i++)
		{
			var (variableName, expression, variableDescription) = variables[i];
			table.AddRow(variableName, "[cyan]" + expression + "[/]", variableDescription);
		}
		AnsiConsole.Write(new Panel(table)
		{
			Header = new PanelHeader("[bold cyan]Variables Configuration[/]"),
			Border = BoxBorder.Rounded
		});
		AnsiConsole.WriteLine();
		if (AnsiConsole.Prompt(
			new SelectionPrompt<string>()
				.Title("[bold]Template Code:[/]")
				.AddChoices("Show template (for copying)", "Next")).Contains("Show"))
		{
			AnsiConsole.Clear();
			AnsiConsole.Write(new Rule("[bold green]" + abbreviation + " - Copy This Template[/]"));
			AnsiConsole.WriteLine();
			Console.WriteLine(template);
			AnsiConsole.WriteLine();
			AnsiConsole.MarkupLine("[dim]Press any key when done copying...[/]");
			Console.ReadKey(intercept: true);
		}
	}

	private static void ShowRiderExternalTools()
	{
		ShowExternalToolSetup("AtomicGenerate", "Regenerates all Entity API and Domain files", "atomic-codegen", "generate", "$ProjectFileDir$", "Ctrl+Shift+G");
		ShowExternalToolSetup("AtomicRename", "Renames symbol at cursor position", "atomic-codegen", "rename-at --file \"$FilePath$\" --line $LineNumber$ --column $ColumnNumber$ --to $Prompt$", "$ProjectFileDir$", "Ctrl+Shift+R");
	}

	private static void ShowExternalToolSetup(string name, string description, string program, string arguments, string workingDir, string suggestedShortcut)
	{
		AnsiConsole.Clear();
		AnsiConsole.Write(new Rule("[bold purple]External Tool: " + name + "[/]"));
		AnsiConsole.WriteLine();
		AnsiConsole.MarkupLine("[bold]" + description + "[/]");
		AnsiConsole.WriteLine();
		AnsiConsole.Write(new Panel(new Markup("[bold]Setting up '" + name + "':[/]\n\n1. Open Rider [yellow]Settings[/] ([dim]Ctrl+Alt+S[/])\n2. Go to [yellow]Tools -> External Tools[/]\n3. Click [green]+[/] to add new tool\n4. Configure as shown below\n5. Click [green]OK[/] to save"))
		{
			Header = new PanelHeader("[bold yellow]Setup Instructions[/]"),
			Border = BoxBorder.Rounded,
			Padding = new Padding(2, 1)
		});
		AnsiConsole.WriteLine();
		Table table = new Table().Border(TableBorder.Rounded).AddColumn("[bold]Setting[/]").AddColumn("[bold]Value[/]");
		table.AddRow("Name", "[bold]" + name + "[/]");
		table.AddRow("Group", "[dim]Atomic CodeGen[/]");
		table.AddRow("Program", "[green]" + program + "[/]");
		table.AddRow("Arguments", "[cyan]" + Markup.Escape(arguments) + "[/]");
		table.AddRow("Working directory", "[blue]" + workingDir + "[/]");
		AnsiConsole.Write(new Panel(table)
		{
			Header = new PanelHeader("[bold green]Tool Configuration[/]"),
			Border = BoxBorder.Rounded
		});
		AnsiConsole.WriteLine();
		AnsiConsole.Write(new Panel(new Markup("[bold]Adding a keyboard shortcut:[/]\n\n1. Go to [yellow]Settings -> Keymap[/]\n2. Search for [green]" + name + "[/]\n3. Right-click -> [yellow]Add Keyboard Shortcut[/]\n4. Suggested: [bold cyan]" + suggestedShortcut + "[/]"))
		{
			Header = new PanelHeader("[bold cyan]Keyboard Shortcut (Optional)[/]"),
			Border = BoxBorder.Rounded,
			Padding = new Padding(2, 1)
		});
		AnsiConsole.WriteLine();
		AnsiConsole.Prompt(
			new SelectionPrompt<string>()
				.Title("[bold]Ready?[/]")
				.AddChoices("Next"));
	}

	private static void ShowComplete(string projectPath)
	{
		AnsiConsole.Clear();
		AnsiConsole.Write(new FigletText("Ready!").LeftJustified().Color(Color.Green));
		AnsiConsole.Write(new Rule("[bold green]Setup Complete![/]"));
		AnsiConsole.WriteLine();
		AnsiConsole.Write(new Panel(new Markup("[bold green]Congratulations![/] Your project is now configured.\n\n[bold]Quick Reference:[/]\n\n  [cyan]atomic-codegen generate[/]     Generate all API files\n  [cyan]atomic-codegen scan[/]         Scan for definitions (dry run)\n  [cyan]atomic-codegen rename[/]       Interactive rename\n  [cyan]atomic-codegen configure[/]    Modify configuration\n  [cyan]atomic-codegen ide[/]          IDE integration help\n\n[bold]Next Steps:[/]\n\n  1. Create an Entity API file with [green][[EntityAPI]][/] attribute\n  2. Run [cyan]atomic-codegen generate[/] to create extension methods\n  3. Use the generated methods in your game code!"))
		{
			Header = new PanelHeader("[bold yellow]You're All Set![/]"),
			Border = BoxBorder.Double,
			Padding = new Padding(2, 1)
		});
		AnsiConsole.WriteLine();
		AnsiConsole.Write(new Panel(new Markup("[dim]Example usage after setup:[/]\n\n[yellow]// Your EntityAPI definition[/]\n[[EntityAPI]]\npublic static partial class PlayerAPI\n{\n    enum Tags { Active, Dead }\n    class Values { int Health; }\n}\n\n[yellow]// Generated extension methods[/]\nentity.IsActive();      [dim]// Check tag[/]\nentity.GetHealth();     [dim]// Get value[/]\nentity.SetHealth(100);  [dim]// Set value[/]"))
		{
			Header = new PanelHeader("[bold cyan]Example[/]"),
			Border = BoxBorder.Rounded,
			Padding = new Padding(2, 1)
		});
		AnsiConsole.WriteLine();
		AnsiConsole.MarkupLine("[dim]Press any key to exit wizard...[/]");
		Console.ReadKey(intercept: true);
	}

	private static bool PromptNavigation(string nextLabel)
	{
		AnsiConsole.WriteLine();
		if (AnsiConsole.Prompt(
			new SelectionPrompt<string>()
				.AddChoices(nextLabel, "Exit wizard")).Contains("Exit"))
		{
			AnsiConsole.MarkupLine("[dim]Wizard cancelled.[/]");
			return false;
		}
		return true;
	}

	private static void ShowParamDescription(string paramName, string description)
	{
		AnsiConsole.Write(new Panel(new Markup(description))
		{
			Header = new PanelHeader("[bold cyan]" + paramName + "[/]"),
			Border = BoxBorder.Rounded,
			Padding = new Padding(2, 1)
		});
		AnsiConsole.WriteLine();
	}

	private static List<string> GetDefaultExcludePaths()
	{
		return new List<string> { "**/obj/**", "**/Library/**", "**/Temp/**", "**/*.g.cs", "**/*.generated.cs" };
	}

	private static string GetEntityApiTemplate()
	{
		return "// ReSharper disable All\r\n#if UNITY_EDITOR\r\n\r\nusing Atomic.Elements;\r\nusing Atomic.Entities;\r\nusing UnityEngine;\r\n\r\nnamespace $NAMESPACE$ {\r\n    [EntityAPI]\r\n    public static partial class $CLASS_NAME$\r\n    {\r\n        enum Tags\r\n        {\r\n            $END$\r\n        }\r\n\r\n        class Values\r\n        {\r\n\r\n        }\r\n    }\r\n}\r\n#endif";
	}

	private static string GetEntityDomainTemplate()
	{
		return "using Atomic.Entities;\r\n\r\n/// <summary>\r\n/// Entity domain definition for $NAME$.\r\n/// </summary>\r\npublic class $NAME$Domain : EntityDomainBuilder\r\n{\r\n    public override string EntityName => \"$NAME$\";\r\n    public override string Namespace => \"$NAMESPACE$\";\r\n    public override string Directory => \"$DIRECTORY$\";\r\n\r\n    public override void Configure()\r\n    {\r\n        // ===== ENTITY MODE (Choose ONE) =====\r\n        EntityMode();\r\n        // EntitySingletonMode();\r\n        // SceneEntityMode();\r\n        // SceneEntitySingletonMode();\r\n\r\n        // ===== SCENE ENTITY OPTIONS =====\r\n        // Only for SceneEntity/SceneEntitySingleton modes:\r\n        // GenerateProxy();\r\n        // GenerateWorld();\r\n\r\n        // ===== INSTALLERS =====\r\n        // IEntityInstaller();\r\n        // ScriptableEntityInstaller();\r\n        // SceneEntityInstaller();\r\n\r\n        // ===== ASPECTS =====\r\n        // ScriptableEntityAspect();\r\n        // SceneEntityAspect();\r\n\r\n        // ===== POOLS =====\r\n        // Only for SceneEntity/SceneEntitySingleton modes:\r\n        // SceneEntityPool();\r\n        // PrefabEntityPool();\r\n\r\n        // ===== FACTORIES =====\r\n        // Only for Entity/EntitySingleton modes:\r\n        // ScriptableEntityFactory();\r\n        // SceneEntityFactory();\r\n\r\n        // ===== BAKERS =====\r\n        // Only for Entity/EntitySingleton modes:\r\n        // StandardBaker();\r\n        // OptimizedBaker();\r\n\r\n        // ===== VIEWS =====\r\n        // Only for Entity/EntitySingleton modes:\r\n        // EntityView();\r\n        // EntityViewCatalog();\r\n        // EntityViewPool();\r\n        // EntityCollectionView();\r\n\r\n        // ===== ADVANCED OPTIONS =====\r\n        // ExcludeImports(\"System\", \"UnityEngine\");\r\n        // TargetProject(\"Assembly-CSharp.csproj\");\r\n    }\r\n}\r\n$END$";
	}
}
