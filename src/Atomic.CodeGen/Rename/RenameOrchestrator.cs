using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Atomic.CodeGen.Core;
using Atomic.CodeGen.Core.Generators.EntityDomain;
using Atomic.CodeGen.Core.Models;
using Atomic.CodeGen.Core.Models.EntityDomain;
using Atomic.CodeGen.Core.Scanners;
using Atomic.CodeGen.Rename.Models;
using Atomic.CodeGen.Rename.UsageFinders;
using Atomic.CodeGen.Roslyn;
using Atomic.CodeGen.Utils;

namespace Atomic.CodeGen.Rename;

public sealed class RenameOrchestrator
{
	private readonly string _projectRoot;

	private readonly CodeGenConfig _config;

	private ApiRegistry? _registry;

	private List<DomainEntry>? _domains;

	private readonly ImportAnalyzer _importAnalyzer = new ImportAnalyzer();

	private readonly Dictionary<RenameType, IUsageFinder> _finders;

	private bool _lastSemanticAnalysisSucceeded;

	public RenameOrchestrator(string projectRoot, CodeGenConfig config)
	{
		_projectRoot = projectRoot;
		_config = config;
		_finders = new Dictionary<RenameType, IUsageFinder>
		{
			{
				RenameType.Tag,
				new TagUsageFinder()
			},
			{
				RenameType.Value,
				new ValueUsageFinder()
			},
			{
				RenameType.Behaviour,
				new BehaviourUsageFinder()
			},
			{
				RenameType.Domain,
				new DomainUsageFinder()
			}
		};
	}

	public async Task<ApiRegistry> GetRegistryAsync()
	{
		if (_registry != null)
		{
			return _registry;
		}
		Logger.LogInfo("Building API registry...");
		List<EntityAPIDefinition> definitions = new List<EntityAPIDefinition>();
		List<BehaviourDefinition> behaviours = new List<BehaviourDefinition>();
		string text = FindSolutionFile(_projectRoot);
		if (text != null)
		{
			Logger.LogVerbose("Using Roslyn semantic analysis for API discovery...");
			List<string> list = _config.ExcludePaths.ToList();
			list.Add("**/.rename-backup/**");
			using SemanticTypeDiscovery discovery = new SemanticTypeDiscovery(text, _config.AnalyzerMode, _config.IncludedProjects);
			DiscoveryResult discoveryResult = await discovery.DiscoverAllAsync(list);
			definitions = discoveryResult.EntityApis.Where((EntityAPIDefinition d) => d.IsValid).ToList();
			behaviours = discoveryResult.Behaviours;
			_domains = discoveryResult.Domains.Values.Select((EntityDomainDefinition d) => new DomainEntry
			{
				ClassName = d.ClassName,
				EntityName = d.EntityName,
				Namespace = d.Namespace,
				Directory = d.Directory,
				SourceFile = d.SourceFile
			}).ToList();
		}
		else
		{
			Logger.LogVerbose("No solution file found, using file scanning...");
			List<string> list2 = new FileScanner(_config).Scan();
			EntityAPIParser parser = new EntityAPIParser();
			BehaviourParser behaviourParser = new BehaviourParser();
			foreach (string item in list2)
			{
				EntityAPIDefinition entityAPIDefinition = await parser.ParseFileAsync(item);
				if (entityAPIDefinition != null)
				{
					definitions.Add(entityAPIDefinition);
				}
			}
			List<string> list3 = (from p in _config.ScanPaths
				select p.Split(new string[1] { "/**" }, StringSplitOptions.None)[0] into p
				where !p.Contains("*")
				select p).Distinct().ToList();
			List<string> includePatterns = ((list3.Count > 0) ? list3.Select((string d) => d.TrimEnd('/', '\\') + "/**/*.cs").ToList() : new List<string> { "**/*.cs" });
			List<string> list4 = _config.ExcludePaths.ToList();
			list4.Add("**/.rename-backup/**");
			List<string> list5 = FileScanner.FindFiles(_projectRoot, includePatterns, list4);
			Logger.LogVerbose($"Scanning {list5.Count} files for behaviours...");
			foreach (string item2 in list5)
			{
				behaviours.AddRange(await behaviourParser.ParseFileAsync(item2));
			}
		}
		foreach (BehaviourDefinition behaviour in behaviours)
		{
			definitions.FirstOrDefault((EntityAPIDefinition d) => d.ClassName == behaviour.LinkedApiTypeName || d.Namespace + "." + d.ClassName == behaviour.LinkedApiTypeName)?.LinkedBehaviours.Add(behaviour);
		}
		_registry = ApiRegistry.Build(definitions);
		Logger.LogInfo($"Found {_registry.AllApis.Count} EntityAPIs");
		return _registry;
	}

	private static string? FindSolutionFile(string projectRoot)
	{
		return Directory.GetFiles(projectRoot, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();
	}

	public async Task<RenameContext> CreateContextAsync(RenameType type, string oldName, string newName, string ownerName, string? ownerNamespace = null, bool renameSourceFile = false)
	{
		ApiRegistry registry = await GetRegistryAsync();
		RenameContext context = new RenameContext
		{
			Type = type,
			OldName = oldName,
			NewName = newName,
			OwnerName = ownerName,
			OwnerNamespace = (ownerNamespace ?? string.Empty),
			SourceFilePath = string.Empty,
			RenameSourceFile = renameSourceFile
		};
		ApiEntry ownerApi = registry.GetByClassName(ownerName);
		if (ownerApi == null && type != RenameType.Domain)
		{
			context.Errors.Add("EntityAPI '" + ownerName + "' not found in project");
			return context;
		}
		if (ownerApi != null)
		{
			context.OwnerNamespace = ownerApi.Namespace;
			context.SourceFilePath = ownerApi.SourceFile;
		}
		if (type == RenameType.Tag && ownerApi != null && !ownerApi.Tags.Contains(oldName))
		{
			context.Errors.Add("Tag '" + oldName + "' not found in " + ownerName);
		}
		else if (type == RenameType.Value && ownerApi != null && !ownerApi.Values.Contains(oldName))
		{
			context.Errors.Add("Value '" + oldName + "' not found in " + ownerName);
		}
		else if (type == RenameType.Behaviour && ownerApi != null)
		{
			BehaviourDefinition behaviourDefinition = ownerApi.BehaviourDefinitions.FirstOrDefault((BehaviourDefinition b) => b.ClassName.Equals(oldName, StringComparison.OrdinalIgnoreCase));
			if (behaviourDefinition == null)
			{
				context.Errors.Add("Behaviour '" + oldName + "' not linked to " + ownerName);
			}
			else
			{
				context.SourceFilePath = behaviourDefinition.SourceFile;
			}
		}
		else if (type == RenameType.Domain)
		{
			DomainEntry domainEntry = await GetDomainByEntityNameAsync(oldName);
			if (domainEntry == null)
			{
				context.Errors.Add("EntityDomain with EntityName '" + oldName + "' not found");
			}
			else
			{
				context.SourceFilePath = domainEntry.SourceFile;
				context.OwnerNamespace = domainEntry.Namespace;
				context.OutputDirectory = domainEntry.Directory;
			}
		}
		if (ownerApi != null)
		{
			ConflictResult conflictResult = registry.CheckConflict(type, ownerName, newName);
			if (conflictResult.HasConflict)
			{
				foreach (string conflict in conflictResult.Conflicts)
				{
					context.Errors.Add(conflict);
				}
			}
		}
		if (!IsValidIdentifier(newName))
		{
			context.Errors.Add("'" + newName + "' is not a valid C# identifier");
		}
		return context;
	}

	public async Task<RenameContext> FindUsagesAsync(RenameContext context)
	{
		if (!context.IsValid)
		{
			return context;
		}
		List<UsageMatch> list = await TrySemanticAnalysisAsync(context);
		if (list.Count > 0 || _lastSemanticAnalysisSucceeded)
		{
			context.UsedSemanticAnalysis = true;
		}
		else
		{
			context.UsedSemanticAnalysis = false;
			List<string> allCsFiles = GetAllCsFiles();
			list = await FallbackToSyntacticAnalysisAsync(context, allCsFiles);
		}
		context.Usages = list;
		if (context.AmbiguousUsages.Count > 0)
		{
			context.Warnings.Add($"{context.AmbiguousUsages.Count} usages are ambiguous and require confirmation");
		}
		return context;
	}

	private List<string> GetAllCsFiles()
	{
		List<string> list = (from p in _config.ScanPaths
			select p.Split(new string[1] { "/**" }, StringSplitOptions.None)[0] into p
			where !p.Contains("*")
			select p).Distinct().ToList();
		List<string> includePatterns = ((list.Count > 0) ? list.Select((string d) => d.TrimEnd('/', '\\') + "/**/*.cs").ToList() : new List<string> { "**/*.cs" });
		return FileScanner.FindFiles(_projectRoot, includePatterns, _config.ExcludePaths);
	}

	private async Task<List<UsageMatch>> TrySemanticAnalysisAsync(RenameContext context)
	{
		_lastSemanticAnalysisSucceeded = false;
		string text = SemanticUsageFinder.FindSolutionFile(_projectRoot);
		if (text == null)
		{
			Logger.LogVerbose("No solution file found, skipping semantic analysis");
			return new List<UsageMatch>();
		}
		Logger.LogVerbose("Found solution: " + text);
		try
		{
			List<UsageMatch> result = await new SemanticUsageFinder(text, _config.AnalyzerMode, _config.IncludedProjects).FindUsagesAsync(context);
			_lastSemanticAnalysisSucceeded = true;
			return result;
		}
		catch (Exception ex)
		{
			Logger.LogWarning("Semantic analysis failed: " + ex.Message);
			Logger.LogVerbose("Stack trace: " + ex.StackTrace);
			return new List<UsageMatch>();
		}
	}

	private async Task<List<UsageMatch>> FallbackToSyntacticAnalysisAsync(RenameContext context, List<string> allCsFiles)
	{
		ApiRegistry registry = await GetRegistryAsync();
		if (!_finders.TryGetValue(context.Type, out IUsageFinder value))
		{
			context.Errors.Add($"No usage finder for type {context.Type}");
			return new List<UsageMatch>();
		}
		List<UsageMatch> list = value.FindUsages(context, allCsFiles, registry, _importAnalyzer);
		if (context.Type == RenameType.Tag || context.Type == RenameType.Value)
		{
			UsageMatch usageMatch = FindSourceDefinition(context);
			if (usageMatch != null)
			{
				list.Insert(0, usageMatch);
			}
		}
		return list;
	}

	public bool Execute(RenameContext context, bool dryRun = false)
	{
		return new RenameExecutor(_projectRoot).Execute(context, dryRun, _config.BackupCap);
	}

	public async Task<bool> RegenerateAffectedApiAsync(RenameContext context)
	{
		if (context.Type == RenameType.Domain)
		{
			return await RegenerateDomainAsync(context);
		}
		string apiSourceFile = (await GetRegistryAsync()).GetByClassName(context.OwnerName)?.SourceFile ?? context.SourceFilePath;
		EntityAPIDefinition definition = await new EntityAPIParser().ParseFileAsync(apiSourceFile);
		if (definition == null || !definition.IsValid)
		{
			Logger.LogWarning("Could not parse API at " + apiSourceFile + " for regeneration");
			return false;
		}
		BehaviourParser behaviourParser = new BehaviourParser();
		List<string> list = _config.ExcludePaths.ToList();
		list.Add("**/.rename-backup/**");
		List<string> list2 = FileScanner.FindFiles(_projectRoot, new List<string> { "**/*.cs" }, list);
		foreach (string item in list2)
		{
			List<BehaviourDefinition> collection = (await behaviourParser.ParseFileAsync(item)).Where((BehaviourDefinition b) => b.LinkedApiTypeName == definition.ClassName || b.LinkedApiTypeName == definition.Namespace + "." + definition.ClassName).ToList();
			definition.LinkedBehaviours.AddRange(collection);
		}
		try
		{
			bool num = await new EntityAPIGenerator(definition, _config).GenerateAsync();
			if (num)
			{
				Logger.LogSuccess("Regenerated " + definition.ClassName);
			}
			return num;
		}
		catch (Exception ex)
		{
			Logger.LogError("Failed to regenerate " + definition.ClassName + ": " + ex.Message);
			return false;
		}
	}

	private async Task<bool> RegenerateDomainAsync(RenameContext context)
	{
		_ = 1;
		try
		{
			EntityDomainDefinition domainDef = await EntityDomainScanner.ParseFileAsync(context.SourceFilePath, _config);
			if (domainDef == null)
			{
				Logger.LogWarning("Could not parse domain definition at " + context.SourceFilePath);
				return false;
			}
			if (!domainDef.EntityName.Equals(context.NewName, StringComparison.OrdinalIgnoreCase))
			{
				Logger.LogWarning($"Domain EntityName is '{domainDef.EntityName}', expected '{context.NewName}'");
			}
			bool num = await new EntityDomainOrchestrator(domainDef, _config).GenerateAsync();
			if (num)
			{
				Logger.LogSuccess("Regenerated EntityDomain: " + domainDef.EntityName);
			}
			return num;
		}
		catch (Exception ex)
		{
			Logger.LogError("Failed to regenerate domain: " + ex.Message);
			return false;
		}
	}

	public List<FileChangeSummary> GetPreview(RenameContext context)
	{
		return RenameExecutor.GetPreview(context);
	}

	private UsageMatch? FindSourceDefinition(RenameContext context)
	{
		if (string.IsNullOrEmpty(context.SourceFilePath) || !File.Exists(context.SourceFilePath))
		{
			return null;
		}
		string[] array = File.ReadAllText(context.SourceFilePath).Split('\n');
		if (context.Type == RenameType.Tag)
		{
			Regex regex = new Regex("\\b" + Regex.Escape(context.OldName) + "\\b");
			bool flag = false;
			for (int i = 0; i < array.Length; i++)
			{
				string text = array[i];
				if (text.Contains("enum Tags"))
				{
					flag = true;
				}
				else if (flag && text.Contains('}'))
				{
					flag = false;
				}
				if (flag)
				{
					Match match = regex.Match(text);
					if (match.Success)
					{
						return new UsageMatch
						{
							FilePath = context.SourceFilePath,
							Line = i + 1,
							Column = match.Index + 1,
							Length = match.Length,
							MatchedText = context.OldName,
							ReplacementText = context.NewName,
							LineContext = text.TrimEnd('\r'),
							Category = "SourceDefinition",
							IsAmbiguous = false
						};
					}
				}
			}
		}
		else if (context.Type == RenameType.Value)
		{
			Regex regex2 = new Regex("\\b" + Regex.Escape(context.OldName) + "\\s*[;=]");
			bool flag2 = false;
			int num = 0;
			for (int j = 0; j < array.Length; j++)
			{
				string text2 = array[j];
				if (text2.Contains("class Values"))
				{
					flag2 = true;
					num = 0;
				}
				if (flag2)
				{
					num += text2.Count((char c) => c == '{');
					num -= text2.Count((char c) => c == '}');
					if (num <= 0 && text2.Contains('}'))
					{
						flag2 = false;
					}
					Match match2 = regex2.Match(text2);
					if (match2.Success)
					{
						return new UsageMatch
						{
							FilePath = context.SourceFilePath,
							Line = j + 1,
							Column = match2.Index + 1,
							Length = context.OldName.Length,
							MatchedText = context.OldName,
							ReplacementText = context.NewName,
							LineContext = text2.TrimEnd('\r'),
							Category = "SourceDefinition",
							IsAmbiguous = false
						};
					}
				}
			}
		}
		return null;
	}

	private static bool IsValidIdentifier(string name)
	{
		if (string.IsNullOrEmpty(name))
		{
			return false;
		}
		if (!char.IsLetter(name[0]) && name[0] != '_')
		{
			return false;
		}
		return name.All((char c) => char.IsLetterOrDigit(c) || c == '_');
	}

	public async Task<ApiEntry?> GetApiSymbolsAsync(string apiName)
	{
		return (await GetRegistryAsync()).GetByClassName(apiName);
	}

	public async Task<IReadOnlyCollection<ApiEntry>> GetAllApisAsync()
	{
		return (await GetRegistryAsync()).AllApis;
	}

	public async Task<IReadOnlyCollection<DomainEntry>> GetAllDomainsAsync()
	{
		if (_domains != null)
		{
			return _domains;
		}
		Logger.LogInfo("Scanning for EntityDomains...");
		string text = FindSolutionFile(_projectRoot);
		if (text != null)
		{
			List<string> list = _config.ExcludePaths.ToList();
			list.Add("**/.rename-backup/**");
			using SemanticTypeDiscovery discovery = new SemanticTypeDiscovery(text, _config.AnalyzerMode, _config.IncludedProjects);
			_domains = (await discovery.DiscoverDomainsAsync(list)).Values.Select((EntityDomainDefinition d) => new DomainEntry
			{
				ClassName = d.ClassName,
				EntityName = d.EntityName,
				Namespace = d.Namespace,
				Directory = d.Directory,
				SourceFile = d.SourceFile
			}).ToList();
		}
		else
		{
			_domains = (await EntityDomainScanner.ScanAsync(_config)).Values.Select((EntityDomainDefinition d) => new DomainEntry
			{
				ClassName = d.ClassName,
				EntityName = d.EntityName,
				Namespace = d.Namespace,
				Directory = d.Directory,
				SourceFile = d.SourceFile
			}).ToList();
		}
		Logger.LogInfo($"Found {_domains.Count} EntityDomain(s)");
		return _domains;
	}

	public async Task<DomainEntry?> GetDomainByEntityNameAsync(string entityName)
	{
		return (await GetAllDomainsAsync()).FirstOrDefault((DomainEntry d) => d.EntityName.Equals(entityName, StringComparison.OrdinalIgnoreCase));
	}
}
