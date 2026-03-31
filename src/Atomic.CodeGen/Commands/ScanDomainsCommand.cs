using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using Atomic.CodeGen.Core.Generators.EntityDomain;
using Atomic.CodeGen.Core.Models;
using Atomic.CodeGen.Core.Models.EntityDomain;
using Atomic.CodeGen.Core.Scanners;
using Atomic.CodeGen.Roslyn;
using Atomic.CodeGen.Utils;
using Spectre.Console;

namespace Atomic.CodeGen.Commands;

public static class ScanDomainsCommand
{
	public static Command Create()
	{
		Option<string> projectOption = new Option<string>(["--project", "-p"], "Project root directory (defaults to current directory)");
		Option<bool> generateOption = new Option<bool>(["--generate", "-g"], () => false, "Generate files (default is scan only)");
		Command command = new Command("scan-domains", "Scan for IEntityDomain implementations") { projectOption, generateOption };
		command.SetHandler(async delegate(string? projectPath, bool generate)
		{
			Logger.LogHeader("Atomic CodeGen - Entity Domain Scanner");
			if (projectPath == null)
			{
				projectPath = Environment.CurrentDirectory;
			}
			CodeGenConfig config = await ConfigLoader.LoadAsync(projectPath);
			string solutionPath = FindSolutionFile(projectPath);
			Dictionary<string, EntityDomainDefinition> definitions;
			if (solutionPath != null)
			{
				Logger.LogInfo("Using Roslyn semantic analysis...");
				Logger.LogInfo("");
				using SemanticTypeDiscovery discovery = new SemanticTypeDiscovery(solutionPath, config.AnalyzerMode, config.IncludedProjects);
				definitions = await discovery.DiscoverDomainsAsync(config.ExcludePaths);
			}
			else
			{
				Logger.LogInfo("No solution file found, using file scanning...");
				definitions = await EntityDomainScanner.ScanAsync(config);
			}
			if (definitions.Count == 0)
			{
				Logger.LogWarning("No IEntityDomain implementations found!");
				Logger.LogInfo("Make sure your classes inherit from EntityDomainBuilder or implement IEntityDomain");
			}
			else
			{
				Logger.LogSuccess($"Found {definitions.Count} EntityDomain definition(s):");
				AnsiConsole.WriteLine();
				foreach (KeyValuePair<string, EntityDomainDefinition> item in definitions)
				{
					var (path, entityDomainDefinition) = item;
					Path.GetRelativePath(config.GetAbsoluteProjectRoot(), path);
					AnsiConsole.Write(new Panel(BuildDomainInfo(entityDomainDefinition, config))
					{
						Header = new PanelHeader("[bold blue]" + entityDomainDefinition.EntityName + "[/]"),
						Border = BoxBorder.Rounded
					});
					AnsiConsole.WriteLine();
				}
				if (generate)
				{
					AnsiConsole.Write(new Rule("[bold]Generating[/]"));
					AnsiConsole.WriteLine();
					int generated = 0;
					foreach (KeyValuePair<string, EntityDomainDefinition> item2 in definitions)
					{
						var (_, definition) = item2;
						try
						{
							if (await new EntityDomainOrchestrator(definition, config).GenerateAsync())
							{
								generated++;
								Logger.LogSuccess("✓ Generated: " + definition.EntityName);
							}
						}
						catch (Exception ex)
						{
							Logger.LogError("✗ Failed: " + definition.EntityName + " - " + ex.Message);
						}
					}
					AnsiConsole.WriteLine();
					AnsiConsole.Write(new Rule("[bold green]Summary[/]"));
					if (generated == definitions.Count)
					{
						Logger.LogSuccess($"Successfully generated {generated}/{definitions.Count} entity domain(s)");
					}
					else
					{
						Logger.LogWarning($"Generated {generated}/{definitions.Count} entity domain(s) (some failed)");
					}
				}
				else
				{
					AnsiConsole.MarkupLine("[dim]Use --generate flag to create files[/]");
				}
			}
		}, projectOption, generateOption);
		return command;
	}

	private static Markup BuildDomainInfo(EntityDomainDefinition definition, CodeGenConfig config)
	{
		List<string> infoLines = new List<string>
		{
			"[dim]Class:[/] " + Markup.Escape(definition.ClassName),
			"[dim]Namespace:[/] " + Markup.Escape(definition.Namespace),
			"[dim]Directory:[/] " + Markup.Escape(definition.Directory),
			$"[dim]Mode:[/] [yellow]{definition.Mode}[/]"
		};
		List<string> features = new List<string>();
		if (definition.GenerateProxy)
		{
			features.Add("Proxy");
		}
		if (definition.GenerateWorld)
		{
			features.Add("World");
		}
		if (definition.Installers != EntityInstallerMode.None)
		{
			features.Add($"Installers({definition.Installers})");
		}
		if (definition.Aspects != EntityAspectMode.None)
		{
			features.Add($"Aspects({definition.Aspects})");
		}
		if (definition.Pools != EntityPoolMode.None)
		{
			features.Add($"Pools({definition.Pools})");
		}
		if (definition.Factories != EntityFactoryMode.None)
		{
			features.Add($"Factories({definition.Factories})");
		}
		if (definition.Bakers != EntityBakerMode.None)
		{
			features.Add($"Bakers({definition.Bakers})");
		}
		if (definition.Views != EntityViewMode.None)
		{
			features.Add($"Views({definition.Views})");
		}
		if (features.Count > 0)
		{
			infoLines.Add("[dim]Features:[/] [green]" + string.Join(", ", features) + "[/]");
		}
		return new Markup(string.Join("\n", infoLines));
	}

	private static string? FindSolutionFile(string projectPath)
	{
		return Directory.GetFiles(projectPath, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();
	}
}
