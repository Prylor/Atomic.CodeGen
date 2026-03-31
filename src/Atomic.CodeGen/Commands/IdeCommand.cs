using System;
using System.CommandLine;
using Spectre.Console;

namespace Atomic.CodeGen.Commands;

public static class IdeCommand
{
	public static Command Create()
	{
		Command command = new Command("ide", "Setup IDE integration (Live Templates, External Tools)");
		command.SetHandler((Action)delegate
		{
			while (true)
			{
				AnsiConsole.Clear();
				AnsiConsole.Write(new FigletText("IDE Setup").LeftJustified().Color(Color.Blue));
				AnsiConsole.Write(new Rule("[bold blue]Atomic CodeGen - IDE Integration[/]"));
				AnsiConsole.WriteLine();
				string text = AnsiConsole.Prompt(new SelectionPrompt<string>().Title("[bold]Select your IDE:[/]").AddChoices("JetBrains Rider", "Exit"));
				if (text.Contains("Exit"))
				{
					break;
				}
				if (text.Contains("Rider"))
				{
					ShowRiderMenu();
				}
			}
		});
		return command;
	}

	private static void ShowRiderMenu()
	{
		while (true)
		{
			AnsiConsole.Clear();
			AnsiConsole.Write(new FigletText("Rider").LeftJustified().Color(Color.Purple));
			AnsiConsole.Write(new Rule("[bold purple]JetBrains Rider Integration[/]"));
			AnsiConsole.WriteLine();
			string text = AnsiConsole.Prompt(new SelectionPrompt<string>().Title("[bold]What would you like to set up?[/]").PageSize(10).AddChoices("Entity API Live Template (eapi)", "Entity Domain Live Template (edom)", "─────────────────────────────────", "Atomic Generate External Tool", "Atomic Rename External Tool", "─────────────────────────────────", "Back to IDE Selection"));
			if (!text.Contains("Back"))
			{
				if (text.Contains("Entity API Live Template"))
				{
					ShowEntityApiTemplate();
				}
				else if (text.Contains("Entity Domain Live Template"))
				{
					ShowEntityDomainTemplate();
				}
				else if (text.Contains("Atomic Generate"))
				{
					ShowGenerateExternalTool();
				}
				else if (text.Contains("Atomic Rename"))
				{
					ShowRenameExternalTool();
				}
				continue;
			}
			break;
		}
	}

	private static void ShowEntityApiTemplate()
	{
		string template = "// ReSharper disable All\r\n#if UNITY_EDITOR\r\n\r\nusing Atomic.Elements;\r\nusing Atomic.Entities;\r\nusing UnityEngine;\r\n\r\nnamespace $NAMESPACE$ {\r\n    [EntityAPI]\r\n    public static partial class $CLASS_NAME$\r\n    {\r\n        enum Tags\r\n        {\r\n\r\n        }\r\n\r\n        class Values\r\n        {\r\n\r\n        }\r\n    }\r\n}\r\n#endif";
		ShowTemplateScreen("Entity API Live Template", "eapi", "Entity API definition", template, new(string, string, string)[3]
		{
			("$NAMESPACE$", "fileDefaultNamespace()", "Current file namespace"),
			("$CLASS_NAME$", "fileNameWithoutExtension()", "File name as class name"),
			("$END$", "(cursor position)", "Final cursor position")
		});
	}

	private static void ShowEntityDomainTemplate()
	{
		string template = "using Atomic.Entities;\r\n\r\n/// <summary>\r\n/// Entity domain definition for $NAME$.\r\n/// </summary>\r\npublic class $NAME$Domain : EntityDomainBuilder\r\n{\r\n    public override string EntityName => \"$NAME$\";\r\n    public override string Namespace => \"$NAMESPACE$\";\r\n    public override string Directory => \"$DIRECTORY$\";\r\n\r\n    public override void Configure()\r\n    {\r\n        // ===== ENTITY MODE (Choose ONE) =====\r\n        EntityMode();\r\n        // EntitySingletonMode();\r\n        // SceneEntityMode();\r\n        // SceneEntitySingletonMode();\r\n\r\n        // ===== SCENE ENTITY OPTIONS =====\r\n        // Only for SceneEntity/SceneEntitySingleton modes:\r\n        // GenerateProxy();\r\n        // GenerateWorld();\r\n\r\n        // ===== INSTALLERS =====\r\n        // IEntityInstaller();\r\n        // ScriptableEntityInstaller();\r\n        // SceneEntityInstaller();\r\n\r\n        // ===== ASPECTS =====\r\n        // ScriptableEntityAspect();\r\n        // SceneEntityAspect();\r\n\r\n        // ===== POOLS =====\r\n        // Only for SceneEntity/SceneEntitySingleton modes:\r\n        // SceneEntityPool();\r\n        // PrefabEntityPool();\r\n\r\n        // ===== FACTORIES =====\r\n        // Only for Entity/EntitySingleton modes:\r\n        // ScriptableEntityFactory();\r\n        // SceneEntityFactory();\r\n\r\n        // ===== BAKERS =====\r\n        // Only for Entity/EntitySingleton modes:\r\n        // StandardBaker();\r\n        // OptimizedBaker();\r\n\r\n        // ===== VIEWS =====\r\n        // Only for Entity/EntitySingleton modes:\r\n        // EntityView();\r\n        // EntityViewCatalog();\r\n        // EntityViewPool();\r\n        // EntityCollectionView();\r\n\r\n        // ===== ADVANCED OPTIONS =====\r\n        // ExcludeImports(\"System\", \"UnityEngine\");\r\n        // TargetProject(\"Assembly-CSharp.csproj\");\r\n    }\r\n}\r\n$END$";
		ShowTemplateScreen("Entity Domain Live Template", "edom", "Entity Domain definition", template, new(string, string, string)[4]
		{
			("$NAME$", "fileNameWithoutExtension()", "Entity name (from file name)"),
			("$NAMESPACE$", "fileDefaultNamespace()", "Current namespace"),
			("$DIRECTORY$", "\"Assets/Scripts/Generated/\" + NAME", "Output directory"),
			("$END$", "(cursor position)", "Final cursor position")
		});
	}

	private static void ShowGenerateExternalTool()
	{
		AnsiConsole.Clear();
		AnsiConsole.Write(new Rule("[bold blue]Atomic Generate External Tool[/]"));
		AnsiConsole.WriteLine();
		AnsiConsole.Write(new Panel(new Markup("[bold]How to add External Tool in Rider:[/]\n\n1. Open [yellow]Settings[/] ([dim]Ctrl+Alt+S[/] or [dim]Cmd+,[/])\n2. Navigate to [yellow]Tools → External Tools[/]\n3. Click [green]+[/] button to add new tool\n4. Fill in the configuration below\n5. Click [green]OK[/] to save"))
		{
			Header = new PanelHeader("[bold yellow]\ud83d\udccd Location[/]"),
			Border = BoxBorder.Rounded,
			Padding = new Padding(2, 1)
		});
		AnsiConsole.WriteLine();
		Table table = new Table().Border(TableBorder.Rounded).AddColumn("[bold]Setting[/]").AddColumn("[bold]Value[/]");
		table.AddRow("[yellow]Name[/]", "[bold]AtomicGenerate[/]");
		table.AddRow("[yellow]Group[/]", "[dim]Atomic CodeGen[/]");
		table.AddRow("[yellow]Program[/]", "[green]atomic-codegen[/]");
		table.AddRow("[yellow]Arguments[/]", "[cyan]generate[/]");
		table.AddRow("[yellow]Working directory[/]", "[blue]$ProjectFileDir$[/]");
		AnsiConsole.Write(new Panel(table)
		{
			Header = new PanelHeader("[bold green]⚙\ufe0f Tool Configuration[/]"),
			Border = BoxBorder.Rounded
		});
		AnsiConsole.WriteLine();
		AnsiConsole.Write(new Panel(new Markup("[bold]How to add Keyboard Shortcut:[/]\n\n1. Open [yellow]Settings[/] ([dim]Ctrl+Alt+S[/] or [dim]Cmd+,[/])\n2. Navigate to [yellow]Keymap[/]\n3. Search for [green]AtomicGenerate[/] (or expand [dim]External Tools[/])\n4. Right-click → [yellow]Add Keyboard Shortcut[/]\n5. Recommended: [bold cyan]Ctrl+Shift+G[/] or [bold cyan]Alt+G[/]"))
		{
			Header = new PanelHeader("[bold cyan]⌨\ufe0f Keyboard Shortcut[/]"),
			Border = BoxBorder.Rounded,
			Padding = new Padding(2, 1)
		});
		AnsiConsole.WriteLine();
		AnsiConsole.Write(new Panel(new Markup("[bold]Usage:[/]\n\n• From menu: [yellow]Tools → External Tools → AtomicGenerate[/]\n• Or use your keyboard shortcut\n• Generates all EntityAPI and EntityDomain files"))
		{
			Header = new PanelHeader("[bold blue]\ud83d\udca1 Usage[/]"),
			Border = BoxBorder.Rounded,
			Padding = new Padding(2, 1)
		});
		AnsiConsole.WriteLine();
		AnsiConsole.MarkupLine("[dim]Press any key to go back...[/]");
		Console.ReadKey(intercept: true);
	}

	private static void ShowRenameExternalTool()
	{
		AnsiConsole.Clear();
		AnsiConsole.Write(new Rule("[bold blue]Atomic Rename External Tool[/]"));
		AnsiConsole.WriteLine();
		AnsiConsole.Write(new Panel(new Markup("[bold]How to add External Tool in Rider:[/]\n\n1. Open [yellow]Settings[/] ([dim]Ctrl+Alt+S[/] or [dim]Cmd+,[/])\n2. Navigate to [yellow]Tools → External Tools[/]\n3. Click [green]+[/] button to add new tool\n4. Fill in the configuration below\n5. Click [green]OK[/] to save"))
		{
			Header = new PanelHeader("[bold yellow]\ud83d\udccd Location[/]"),
			Border = BoxBorder.Rounded,
			Padding = new Padding(2, 1)
		});
		AnsiConsole.WriteLine();
		Table table = new Table().Border(TableBorder.Rounded).AddColumn("[bold]Setting[/]").AddColumn("[bold]Value[/]");
		table.AddRow("[yellow]Name[/]", "[bold]AtomicRename[/]");
		table.AddRow("[yellow]Group[/]", "[dim]Atomic CodeGen[/]");
		table.AddRow("[yellow]Program[/]", "[green]atomic-codegen[/]");
		table.AddRow("[yellow]Arguments[/]", "[cyan]rename-at --file \"$FilePath$\" --line $LineNumber$ --column $ColumnNumber$ --to $Prompt$[/]");
		table.AddRow("[yellow]Working directory[/]", "[blue]$ProjectFileDir$[/]");
		AnsiConsole.Write(new Panel(table)
		{
			Header = new PanelHeader("[bold green]⚙\ufe0f Tool Configuration[/]"),
			Border = BoxBorder.Rounded
		});
		AnsiConsole.WriteLine();
		AnsiConsole.Write(new Panel(new Markup("[bold]Arguments explained:[/]\n\n[yellow]--file \"$FilePath$\"[/]     Current file path\n[yellow]--line $LineNumber$[/]      Cursor line number\n[yellow]--column $ColumnNumber$[/]  Cursor column number\n[yellow]--to $Prompt$[/]            Opens dialog for new name"))
		{
			Header = new PanelHeader("[bold cyan]\ud83d\udcdd Arguments[/]"),
			Border = BoxBorder.Rounded,
			Padding = new Padding(2, 1)
		});
		AnsiConsole.WriteLine();
		AnsiConsole.Write(new Panel(new Markup("[bold]How to add Keyboard Shortcut:[/]\n\n1. Open [yellow]Settings[/] ([dim]Ctrl+Alt+S[/] or [dim]Cmd+,[/])\n2. Navigate to [yellow]Keymap[/]\n3. Search for [green]AtomicRename[/] (or expand [dim]External Tools[/])\n4. Right-click → [yellow]Add Keyboard Shortcut[/]\n5. Recommended: [bold cyan]Ctrl+Shift+R[/] or [bold cyan]Alt+R[/]"))
		{
			Header = new PanelHeader("[bold cyan]⌨\ufe0f Keyboard Shortcut[/]"),
			Border = BoxBorder.Rounded,
			Padding = new Padding(2, 1)
		});
		AnsiConsole.WriteLine();
		AnsiConsole.Write(new Panel(new Markup("[bold]Usage:[/]\n\n1. Place cursor on a [yellow]Tag[/], [yellow]Value[/], or [yellow]Behaviour[/] name\n2. Press your keyboard shortcut or use menu\n3. Enter the new name in the prompt dialog\n4. Tool renames all usages + regenerates API\n\n[dim]Works with:[/] Tags, Values, Behaviours, Domains"))
		{
			Header = new PanelHeader("[bold blue]\ud83d\udca1 Usage[/]"),
			Border = BoxBorder.Rounded,
			Padding = new Padding(2, 1)
		});
		AnsiConsole.WriteLine();
		AnsiConsole.MarkupLine("[dim]Press any key to go back...[/]");
		Console.ReadKey(intercept: true);
	}

	private static void ShowTemplateScreen(string title, string abbreviation, string description, string template, (string Variable, string Expression, string Description)[] variables)
	{
		while (true)
		{
			AnsiConsole.Clear();
			AnsiConsole.Write(new Rule("[bold blue]" + title + "[/]"));
			AnsiConsole.WriteLine();
			AnsiConsole.Write(new Panel(new Markup("[bold]How to add Live Template in Rider:[/]\n\n1. Open [yellow]Settings[/] ([dim]Ctrl+Alt+S[/] or [dim]Cmd+,[/])\n2. Navigate to [yellow]Editor -> Live Templates[/]\n3. Select [yellow]C#[/] group (or create new group)\n4. Click [green]+[/] button -> [green]Live Template[/]\n5. Set [yellow]Abbreviation[/]: [bold green]" + abbreviation + "[/]\n6. Set [yellow]Description[/]: [dim]" + description + "[/]\n7. Paste the template below\n8. Click [yellow]Edit variables[/] to configure placeholders\n9. Set [yellow]Applicable in[/]: C# -> Everywhere"))
			{
				Header = new PanelHeader("[bold yellow]Setup Instructions[/]"),
				Border = BoxBorder.Rounded,
				Padding = new Padding(2, 1)
			});
			AnsiConsole.WriteLine();
			AnsiConsole.Write(new Panel(new Text(template))
			{
				Header = new PanelHeader("[bold green]Template Code[/]"),
				Border = BoxBorder.Rounded,
				Padding = new Padding(2, 1)
			});
			AnsiConsole.WriteLine();
			Table table = new Table().Border(TableBorder.Rounded).AddColumn("[bold]Variable[/]").AddColumn("[bold]Expression[/]")
				.AddColumn("[bold]Description[/]");
			for (int i = 0; i < variables.Length; i++)
			{
				var (text, text2, text3) = variables[i];
				table.AddRow(text, "[dim]" + text2 + "[/]", text3);
			}
			AnsiConsole.Write(new Panel(table)
			{
				Header = new PanelHeader("[bold cyan]Variables Configuration[/]"),
				Border = BoxBorder.Rounded
			});
			AnsiConsole.WriteLine();
			string text4 = AnsiConsole.Prompt(new SelectionPrompt<string>().Title("[bold]Options:[/]").AddChoices("Show raw (for copying)", "Back"));
			if (!text4.Contains("Back"))
			{
				if (text4.Contains("raw"))
				{
					AnsiConsole.Clear();
					AnsiConsole.Write(new Rule("[bold blue]" + title + " - Raw Template[/]"));
					AnsiConsole.WriteLine();
					AnsiConsole.MarkupLine("[bold green]Copy the text below:[/]");
					AnsiConsole.WriteLine();
					Console.WriteLine(template);
					AnsiConsole.WriteLine();
					AnsiConsole.MarkupLine("[dim]Press any key to go back...[/]");
					Console.ReadKey(intercept: true);
				}
				continue;
			}
			break;
		}
	}
}
