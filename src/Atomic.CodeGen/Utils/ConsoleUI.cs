using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Atomic.CodeGen.Rename;
using Atomic.CodeGen.Rename.Models;
using Spectre.Console;

namespace Atomic.CodeGen.Utils;

public static class ConsoleUI
{
	public enum RenameCategory
	{
		EntityAPI,
		Behaviour,
		Domain
	}

	public static RenameCategory SelectRenameCategory()
	{
		string text = AnsiConsole.Prompt(new SelectionPrompt<string>().Title("[bold]What do you want to rename?[/]").PageSize(10).AddChoices("EntityAPI Tag or Value", "Behaviour", "Domain"));
		if (text.StartsWith("EntityAPI"))
		{
			return RenameCategory.EntityAPI;
		}
		if (text.StartsWith("Behaviour"))
		{
			return RenameCategory.Behaviour;
		}
		if (text.StartsWith("Domain"))
		{
			return RenameCategory.Domain;
		}
		return RenameCategory.EntityAPI;
	}

	public static RenameType SelectTagOrValue()
	{
		if (!(AnsiConsole.Prompt(new SelectionPrompt<string>().Title("[bold]Rename Tag or Value?[/]").AddChoices("Tag", "Value")) == "Tag"))
		{
			return RenameType.Value;
		}
		return RenameType.Tag;
	}

	public static ApiEntry? SelectApi(IReadOnlyCollection<ApiEntry> apis)
	{
		if (apis.Count == 0)
		{
			AnsiConsole.MarkupLine("[red]No EntityAPIs found in project[/]");
			return null;
		}
		string text = AnsiConsole.Prompt(new SelectionPrompt<string>().Title("[bold]Select EntityAPI:[/]").PageSize(15).AddChoices(apis.Select((ApiEntry a) => a.ClassName + " (" + a.Namespace + ")")));
		string className = text.Split(' ')[0];
		return apis.FirstOrDefault((ApiEntry a) => a.ClassName == className);
	}

	public static (ApiEntry api, string behaviourName)? SelectBehaviour(IReadOnlyCollection<ApiEntry> apis)
	{
		List<(ApiEntry, string, string)> list = new List<(ApiEntry, string, string)>();
		foreach (ApiEntry api in apis)
		{
			foreach (string behaviour in api.Behaviours)
			{
				list.Add((api, behaviour, behaviour + " (linked to " + api.ClassName + ")"));
			}
		}
		if (list.Count == 0)
		{
			AnsiConsole.MarkupLine("[red]No behaviours found. Use [[LinkTo]] attribute to link behaviours to EntityAPIs.[/]");
			return null;
		}
		string choice = AnsiConsole.Prompt(new SelectionPrompt<string>().Title("[bold]Select Behaviour to rename:[/]").PageSize(15).AddChoices(list.Select<(ApiEntry, string, string), string>(((ApiEntry api, string behaviour, string display) b) => b.display)));
		(ApiEntry, string, string) tuple = list.FirstOrDefault<(ApiEntry, string, string)>(((ApiEntry api, string behaviour, string display) b) => b.display == choice);
		return (tuple.Item1, tuple.Item2);
	}

	public static DomainEntry? SelectDomain(IReadOnlyCollection<DomainEntry> domains)
	{
		if (domains.Count == 0)
		{
			AnsiConsole.MarkupLine("[red]No EntityDomains found in project[/]");
			return null;
		}
		string text = AnsiConsole.Prompt(new SelectionPrompt<string>().Title("[bold]Select EntityDomain:[/]").PageSize(15).AddChoices(domains.Select((DomainEntry d) => d.EntityName + " (" + d.ClassName + ")")));
		string entityName = text.Split(' ')[0];
		return domains.FirstOrDefault((DomainEntry d) => d.EntityName == entityName);
	}

	public static string? SelectSymbol(ApiEntry api, RenameType type)
	{
		List<string> list = type switch
		{
			RenameType.Tag => api.Tags.ToList(), 
			RenameType.Value => api.Values.ToList(), 
			RenameType.Behaviour => api.Behaviours.ToList(), 
			_ => new List<string>(), 
		};
		if (list.Count == 0)
		{
			AnsiConsole.MarkupLine($"[red]No {type}s found in {api.ClassName}[/]");
			return null;
		}
		return AnsiConsole.Prompt(new SelectionPrompt<string>().Title($"[bold]Select {type} to rename:[/]").PageSize(15).AddChoices(list));
	}

	public static string PromptNewName(string oldName)
	{
		return AnsiConsole.Prompt(new TextPrompt<string>("[bold]New name[/] (current: [yellow]" + oldName + "[/]):").Validate(delegate(string name)
		{
			if (string.IsNullOrWhiteSpace(name))
			{
				return ValidationResult.Error("Name cannot be empty");
			}
			if (!char.IsLetter(name[0]) && name[0] != '_')
			{
				return ValidationResult.Error("Name must start with a letter or underscore");
			}
			return (!name.All((char c) => char.IsLetterOrDigit(c) || c == '_')) ? ValidationResult.Error("Name can only contain letters, digits, and underscores") : ValidationResult.Success();
		}));
	}

	public static void ShowPreview(RenameContext context, List<FileChangeSummary> preview)
	{
		AnsiConsole.WriteLine();
		AnsiConsole.Write(new Rule($"[bold blue]Rename Preview: {context.OldName} -> {context.NewName}[/]"));
		AnsiConsole.WriteLine();
		Table table = new Table().Border(TableBorder.Rounded).AddColumn("File").AddColumn("Changes")
			.AddColumn("Categories");
		foreach (FileChangeSummary item3 in preview)
		{
			string fileName = Path.GetFileName(item3.FilePath);
			string value = ((item3.AmbiguousCount > 0) ? $" [yellow]({item3.AmbiguousCount} ambiguous)[/]" : "");
			table.AddRow(Markup.Escape(fileName), $"{item3.ChangeCount}{value}", Markup.Escape(string.Join(", ", item3.Categories.Take(3))));
		}
		AnsiConsole.Write(table);
		AnsiConsole.WriteLine();
		if (context.FileRenames.Count > 0)
		{
			AnsiConsole.MarkupLine("[bold]Files to rename:[/]");
			foreach (var fileRename in context.FileRenames)
			{
				string item = fileRename.OldPath;
				string item2 = fileRename.NewPath;
				string fileName2 = Path.GetFileName(item);
				string fileName3 = Path.GetFileName(item2);
				AnsiConsole.MarkupLine($"  [yellow]{Markup.Escape(fileName2)}[/] -> [green]{Markup.Escape(fileName3)}[/]");
			}
			AnsiConsole.WriteLine();
		}
		if (context.AmbiguousUsages.Count <= 0)
		{
			return;
		}
		AnsiConsole.MarkupLine("[yellow]Some usages are ambiguous (multiple APIs have this symbol):[/]");
		foreach (UsageMatch item4 in context.AmbiguousUsages.Take(5))
		{
			AnsiConsole.MarkupLine($"  [dim]{Markup.Escape(Path.GetFileName(item4.FilePath))}:{item4.Line}[/] - Could be: {Markup.Escape(string.Join(", ", item4.PossibleApis ?? new List<string>()))}");
		}
		if (context.AmbiguousUsages.Count > 5)
		{
			AnsiConsole.MarkupLine($"  [dim]...and {context.AmbiguousUsages.Count - 5} more[/]");
		}
		AnsiConsole.WriteLine();
	}

	public static void ShowDetailedUsages(RenameContext context)
	{
		List<Markup> list = (from u in context.Usages.Take(20)
			select new Markup($"[dim]{Markup.Escape(Path.GetFileName(u.FilePath))}:{u.Line}:{u.Column}[/] [blue]{Markup.Escape(u.MatchedText)}[/] -> [green]{Markup.Escape(u.ReplacementText)}[/]")).ToList();
		if (list.Count > 0)
		{
			AnsiConsole.Write(new Panel(new Rows(list))
			{
				Header = new PanelHeader("[bold]Usage Details[/]"),
				Border = BoxBorder.Rounded
			});
		}
		if (context.Usages.Count > 20)
		{
			AnsiConsole.MarkupLine($"[dim]...and {context.Usages.Count - 20} more usages[/]");
		}
	}

	public static bool PromptRenameFile(string currentFileName, string newFileName)
	{
		return AnsiConsole.Confirm($"Also rename file [yellow]{Markup.Escape(currentFileName)}[/] to [green]{Markup.Escape(newFileName)}[/]?");
	}

	public static bool ConfirmRename(RenameContext context)
	{
		int count = context.CertainUsages.Count;
		int count2 = context.AmbiguousUsages.Count;
		string text = $"Rename [yellow]{Markup.Escape(context.OldName)}[/] to [green]{Markup.Escape(context.NewName)}[/]? ({count} certain usages";
		if (count2 > 0)
		{
			text += $", {count2} ambiguous - will be skipped";
		}
		text += ")";
		return AnsiConsole.Confirm(text);
	}

	public static List<UsageMatch> ConfirmAmbiguousUsages(RenameContext context)
	{
		if (context.AmbiguousUsages.Count == 0)
		{
			return new List<UsageMatch>();
		}
		AnsiConsole.MarkupLine("[yellow]Review ambiguous usages:[/]");
		List<UsageMatch> list = new List<UsageMatch>();
		foreach (UsageMatch ambiguousUsage in context.AmbiguousUsages)
		{
			AnsiConsole.WriteLine();
			AnsiConsole.MarkupLine($"[bold]{Markup.Escape(Path.GetFileName(ambiguousUsage.FilePath))}:{ambiguousUsage.Line}:{ambiguousUsage.Column}[/]");
			AnsiConsole.MarkupLine("[dim]" + Markup.Escape(ambiguousUsage.LineContext?.Trim() ?? "") + "[/]");
			AnsiConsole.MarkupLine("Possible APIs: " + Markup.Escape(string.Join(", ", ambiguousUsage.PossibleApis ?? new List<string>())));
			if (AnsiConsole.Confirm("Rename this usage?", defaultValue: false))
			{
				list.Add(ambiguousUsage with
				{
					IsAmbiguous = false
				});
			}
		}
		return list;
	}

	public static void ShowSuccess(RenameContext context)
	{
		AnsiConsole.Write(new Rule("[green]Rename Successful[/]"));
		AnsiConsole.MarkupLine($"[green]✓[/] Renamed [yellow]{Markup.Escape(context.OldName)}[/] to [green]{Markup.Escape(context.NewName)}[/]");
		AnsiConsole.MarkupLine($"[green]✓[/] Modified {context.AffectedFiles.Count()} files");
		AnsiConsole.MarkupLine("[green]✓[/] API regenerated");
	}

	public static void ShowErrors(RenameContext context)
	{
		AnsiConsole.Write(new Rule("[red]Rename Failed[/]"));
		foreach (string error in context.Errors)
		{
			AnsiConsole.MarkupLine("[red]✗[/] " + Markup.Escape(error));
		}
	}

	public static void ShowWarnings(RenameContext context)
	{
		if (context.Warnings.Count == 0)
		{
			return;
		}
		foreach (string warning in context.Warnings)
		{
			AnsiConsole.MarkupLine("[yellow]![/] " + Markup.Escape(warning));
		}
	}

	public static T WithSpinner<T>(string message, Func<T> action)
	{
		return AnsiConsole.Status().Spinner(Spinner.Known.Dots).Start(message, (StatusContext ctx) => action());
	}

	public static async Task<T> WithSpinnerAsync<T>(string message, Func<Task<T>> action)
	{
		return await AnsiConsole.Status().Spinner(Spinner.Known.Dots).StartAsync(message, async (StatusContext ctx) => await action());
	}
}
