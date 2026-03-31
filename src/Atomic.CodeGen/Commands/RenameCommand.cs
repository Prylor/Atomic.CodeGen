using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using Atomic.CodeGen.Core.Models;
using Atomic.CodeGen.Rename;
using Atomic.CodeGen.Rename.Models;
using Atomic.CodeGen.Utils;

namespace Atomic.CodeGen.Commands;

public static class RenameCommand
{
	public static Command Create()
	{
		Option<string> projectOption = new Option<string>(new string[2] { "--project", "-p" }, () => Directory.GetCurrentDirectory(), "Path to project root");
		Option<string?> typeOption = new Option<string>(new string[2] { "--type", "-t" }, "Type to rename: tag, value, behaviour, domain");
		Option<string?> apiOption = new Option<string>(new string[2] { "--api", "-a" }, "EntityAPI class name (required for tag/value/behaviour)");
		Option<string?> nameOption = new Option<string>(new string[2] { "--name", "-n" }, "Current symbol name to rename");
		Option<string?> toOption = new Option<string>(new string[1] { "--to" }, "New name for the symbol");
		Option<bool> dryRunOption = new Option<bool>(new string[2] { "--dry-run", "-d" }, () => false, "Preview changes without applying them");
		Option<bool> renameFileOption = new Option<bool>(new string[1] { "--rename-file" }, () => false, "Also rename the source file (for behaviours)");
		Option<bool> verboseOption = new Option<bool>(new string[2] { "--verbose", "-v" }, () => false, "Enable verbose logging");
		Option<bool> yesOption = new Option<bool>(new string[2] { "--yes", "-y" }, () => false, "Skip confirmation prompt");
		Command obj = new Command("rename", "Rename EntityAPI symbols (Tags, Values, Behaviours, Domains)") { projectOption, typeOption, apiOption, nameOption, toOption, dryRunOption, renameFileOption, verboseOption, yesOption };
		obj.SetHandler(async delegate(InvocationContext ctx)
		{
			string? valueForOption = ctx.ParseResult.GetValueForOption(projectOption);
			string type = ctx.ParseResult.GetValueForOption(typeOption);
			string api = ctx.ParseResult.GetValueForOption(apiOption);
			string name = ctx.ParseResult.GetValueForOption(nameOption);
			string to = ctx.ParseResult.GetValueForOption(toOption);
			bool dryRun = ctx.ParseResult.GetValueForOption(dryRunOption);
			bool renameFile = ctx.ParseResult.GetValueForOption(renameFileOption);
			bool verbose = ctx.ParseResult.GetValueForOption(verboseOption);
			bool yes = ctx.ParseResult.GetValueForOption(yesOption);
			Logger.SetVerbose(verbose);
			CodeGenConfig codeGenConfig = await ConfigLoader.LoadAsync(valueForOption);
			RenameOrchestrator orchestrator = new RenameOrchestrator(codeGenConfig.GetAbsoluteProjectRoot(), codeGenConfig);
			bool isInteractive = type == null || name == null || to == null;
			RenameType renameType;
			string oldName;
			string ownerName;
			string newName;
			if (isInteractive)
			{
				Logger.LogHeader("Atomic CodeGen - Rename Wizard");
				IReadOnlyCollection<ApiEntry> readOnlyCollection = await ConsoleUI.WithSpinnerAsync("Loading EntityAPIs...", async () => await orchestrator.GetAllApisAsync());
				if (readOnlyCollection.Count == 0)
				{
					Logger.LogError("No EntityAPIs found in project");
					return;
				}
				switch (ConsoleUI.SelectRenameCategory())
				{
				default:
					return;
				case ConsoleUI.RenameCategory.EntityAPI:
				{
					ApiEntry apiEntry = ConsoleUI.SelectApi(readOnlyCollection);
					if (apiEntry == null)
					{
						return;
					}
					renameType = ConsoleUI.SelectTagOrValue();
					string text = ConsoleUI.SelectSymbol(apiEntry, renameType);
					if (text == null)
					{
						return;
					}
					oldName = text;
					ownerName = apiEntry.ClassName;
					break;
				}
				case ConsoleUI.RenameCategory.Behaviour:
				{
					(ApiEntry, string)? tuple = ConsoleUI.SelectBehaviour(readOnlyCollection);
					if (!tuple.HasValue)
					{
						return;
					}
					renameType = RenameType.Behaviour;
					ApiEntry apiEntry = tuple.Value.Item1;
					oldName = tuple.Value.Item2;
					ownerName = apiEntry.ClassName;
					break;
				}
				case ConsoleUI.RenameCategory.Domain:
				{
					IReadOnlyCollection<DomainEntry> readOnlyCollection2 = await ConsoleUI.WithSpinnerAsync("Loading EntityDomains...", async () => await orchestrator.GetAllDomainsAsync());
					if (readOnlyCollection2.Count == 0)
					{
						Logger.LogError("No EntityDomains found in project");
						return;
					}
					DomainEntry domainEntry = ConsoleUI.SelectDomain(readOnlyCollection2);
					if (domainEntry == null)
					{
						return;
					}
					renameType = RenameType.Domain;
					oldName = domainEntry.EntityName;
					ownerName = domainEntry.ClassName;
					break;
				}
				}
				newName = ConsoleUI.PromptNewName(oldName);
			}
			else
			{
				Logger.LogHeader("Atomic CodeGen - Rename");
				renameType = ParseRenameType(type);
				if (renameType != RenameType.Domain && string.IsNullOrEmpty(api))
				{
					throw new ArgumentException("--api is required for tag/value/behaviour renames");
				}
				ownerName = api ?? name;
				oldName = name;
				newName = to;
			}
			RenameContext context = await ConsoleUI.WithSpinnerAsync("Validating rename...", async () => await orchestrator.CreateContextAsync(renameType, oldName, newName, ownerName, null, renameFile));
			if (!context.IsValid)
			{
				ConsoleUI.ShowErrors(context);
			}
			else
			{
				context = await ConsoleUI.WithSpinnerAsync("Finding usages...", async () => await orchestrator.FindUsagesAsync(context));
				if (context.UsedSemanticAnalysis)
				{
					Logger.LogInfo("Using Roslyn semantic analysis for precise type resolution");
				}
				else
				{
					Logger.LogInfo("Using syntactic analysis (fallback)");
				}
				Logger.LogInfo($"Found {context.Usages.Count} usages in {context.AffectedFiles.Count()} files");
				if (!context.IsValid)
				{
					ConsoleUI.ShowErrors(context);
				}
				else
				{
					ConsoleUI.ShowWarnings(context);
					List<FileChangeSummary> preview = orchestrator.GetPreview(context);
					ConsoleUI.ShowPreview(context, preview);
					if (verbose)
					{
						ConsoleUI.ShowDetailedUsages(context);
					}
					if (isInteractive && renameType == RenameType.Behaviour && !renameFile)
					{
						string sourceFilePath = context.SourceFilePath;
						if (!string.IsNullOrEmpty(sourceFilePath) && File.Exists(sourceFilePath))
						{
							string fileName = Path.GetFileName(sourceFilePath);
							string text2 = fileName.Replace(oldName, newName);
							if (fileName != text2 && ConsoleUI.PromptRenameFile(fileName, text2))
							{
								context.RenameSourceFile = true;
							}
						}
					}
					if (isInteractive && context.AmbiguousUsages.Count > 0)
					{
						foreach (UsageMatch usage in ConsoleUI.ConfirmAmbiguousUsages(context))
						{
							UsageMatch usageMatch = context.Usages.FirstOrDefault((UsageMatch u) => u.FilePath == usage.FilePath && u.Line == usage.Line && u.Column == usage.Column);
							if (usageMatch != null)
							{
								context.Usages.Remove(usageMatch);
								context.Usages.Add(usage);
							}
						}
					}
					if (dryRun)
					{
						Logger.LogInfo("Dry run complete - no changes made");
					}
					else if (!yes && !ConsoleUI.ConfirmRename(context))
					{
						Logger.LogInfo("Rename cancelled");
					}
					else if (ConsoleUI.WithSpinner("Applying changes...", () => orchestrator.Execute(context)))
					{
						if (!(await ConsoleUI.WithSpinnerAsync("Regenerating API...", async () => await orchestrator.RegenerateAffectedApiAsync(context))))
						{
							Logger.LogWarning("API regeneration failed. Run 'atomic-codegen generate' manually.");
						}
						ConsoleUI.ShowSuccess(context);
					}
					else
					{
						Logger.LogError("Rename failed - changes have been rolled back");
					}
				}
			}
		});
		return obj;
	}

	private static RenameType ParseRenameType(string type)
	{
		switch (type.ToLowerInvariant())
		{
		case "tag":
			return RenameType.Tag;
		case "value":
			return RenameType.Value;
		case "behaviour":
		case "behavior":
			return RenameType.Behaviour;
		case "domain":
			return RenameType.Domain;
		default:
			throw new ArgumentException("Unknown type: " + type + ". Use: tag, value, behaviour, domain");
		}
	}
}
