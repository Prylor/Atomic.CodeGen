using System;
using System.Collections.Generic;
using System.Linq;
using Atomic.CodeGen.Core.Models;
using Atomic.CodeGen.Rename.Models;

namespace Atomic.CodeGen.Rename;

public sealed class ApiRegistry
{
	private readonly Dictionary<string, ApiEntry> _apisByClassName = new Dictionary<string, ApiEntry>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, ApiEntry> _apisByFullName = new Dictionary<string, ApiEntry>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, List<ApiEntry>> _apisByTag = new Dictionary<string, List<ApiEntry>>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, List<ApiEntry>> _apisByValue = new Dictionary<string, List<ApiEntry>>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, List<ApiEntry>> _apisByBehaviour = new Dictionary<string, List<ApiEntry>>(StringComparer.OrdinalIgnoreCase);

	public IReadOnlyCollection<ApiEntry> AllApis => _apisByClassName.Values;

	public static ApiRegistry Build(IEnumerable<EntityAPIDefinition> definitions)
	{
		ApiRegistry apiRegistry = new ApiRegistry();
		foreach (EntityAPIDefinition definition in definitions)
		{
			if (!definition.IsValid)
			{
				continue;
			}
			ApiEntry apiEntry = new ApiEntry
			{
				ClassName = definition.ClassName,
				Namespace = definition.Namespace,
				FullName = definition.Namespace + "." + definition.ClassName,
				SourceFile = definition.SourceFile,
				EntityType = definition.EntityType,
				Tags = definition.Tags.ToHashSet<string>(StringComparer.OrdinalIgnoreCase),
				Values = definition.Values.Keys.ToHashSet<string>(StringComparer.OrdinalIgnoreCase),
				Behaviours = (from b in definition.LinkedBehaviours
					where b.IsValid
					select b.ClassName).ToHashSet<string>(StringComparer.OrdinalIgnoreCase),
				BehaviourDefinitions = definition.LinkedBehaviours.Where((BehaviourDefinition b) => b.IsValid).ToList()
			};
			apiRegistry._apisByClassName[apiEntry.ClassName] = apiEntry;
			apiRegistry._apisByFullName[apiEntry.FullName] = apiEntry;
			foreach (string tag in apiEntry.Tags)
			{
				if (!apiRegistry._apisByTag.TryGetValue(tag, out List<ApiEntry> tagApis))
				{
					tagApis = new List<ApiEntry>();
					apiRegistry._apisByTag[tag] = tagApis;
				}
				tagApis.Add(apiEntry);
			}
			foreach (string valueName in apiEntry.Values)
			{
				if (!apiRegistry._apisByValue.TryGetValue(valueName, out List<ApiEntry> valueApis))
				{
					valueApis = new List<ApiEntry>();
					apiRegistry._apisByValue[valueName] = valueApis;
				}
				valueApis.Add(apiEntry);
			}
			foreach (string behaviour in apiEntry.Behaviours)
			{
				if (!apiRegistry._apisByBehaviour.TryGetValue(behaviour, out List<ApiEntry> behaviourApis))
				{
					behaviourApis = new List<ApiEntry>();
					apiRegistry._apisByBehaviour[behaviour] = behaviourApis;
				}
				behaviourApis.Add(apiEntry);
			}
		}
		return apiRegistry;
	}

	public ApiEntry? GetByClassName(string className)
	{
		return _apisByClassName.GetValueOrDefault(className);
	}

	public ApiEntry? GetByFullName(string fullName)
	{
		return _apisByFullName.GetValueOrDefault(fullName);
	}

	public IReadOnlyList<ApiEntry> GetApisWithTag(string tagName)
	{
		if (!_apisByTag.TryGetValue(tagName, out List<ApiEntry> tagApis))
		{
			return new List<ApiEntry>();
		}
		return tagApis;
	}

	public IReadOnlyList<ApiEntry> GetApisWithValue(string valueName)
	{
		if (!_apisByValue.TryGetValue(valueName, out List<ApiEntry> valueApis))
		{
			return new List<ApiEntry>();
		}
		return valueApis;
	}

	public IReadOnlyList<ApiEntry> GetApisWithBehaviour(string behaviourName)
	{
		if (!_apisByBehaviour.TryGetValue(behaviourName, out List<ApiEntry> behaviourApis))
		{
			return new List<ApiEntry>();
		}
		return behaviourApis;
	}

	public List<ApiEntry> GetAccessibleApis(IEnumerable<string> usingNamespaces)
	{
		HashSet<string> namespaceSet = usingNamespaces.ToHashSet<string>(StringComparer.OrdinalIgnoreCase);
		return _apisByClassName.Values.Where((ApiEntry api) => namespaceSet.Contains(api.Namespace)).ToList();
	}

	public AmbiguityResult CheckAmbiguity(RenameType type, string symbolName, IEnumerable<string> accessibleNamespaces)
	{
		List<ApiEntry> accessibleApis = GetAccessibleApis(accessibleNamespaces);
		List<ApiEntry> matchingApis = type switch
		{
			RenameType.Tag => accessibleApis.Where((ApiEntry a) => a.Tags.Contains(symbolName)).ToList(),
			RenameType.Value => accessibleApis.Where((ApiEntry a) => a.Values.Contains(symbolName)).ToList(),
			RenameType.Behaviour => accessibleApis.Where((ApiEntry a) => a.Behaviours.Contains(symbolName)).ToList(),
			_ => new List<ApiEntry>(),
		};
		return new AmbiguityResult
		{
			IsAmbiguous = (matchingApis.Count > 1),
			MatchingApis = matchingApis
		};
	}

	public ConflictResult CheckConflict(RenameType type, string apiClassName, string newName)
	{
		ApiEntry byClassName = GetByClassName(apiClassName);
		if (byClassName == null)
		{
			return new ConflictResult
			{
				HasConflict = false
			};
		}
		List<string> conflicts = new List<string>();
		if (byClassName.Tags.Contains(newName))
		{
			conflicts.Add($"Tag '{newName}' already exists in {apiClassName}");
		}
		if (byClassName.Values.Contains(newName))
		{
			conflicts.Add($"Value '{newName}' already exists in {apiClassName}");
		}
		if (byClassName.Behaviours.Contains(newName))
		{
			conflicts.Add($"Behaviour '{newName}' already exists in {apiClassName}");
		}
		return new ConflictResult
		{
			HasConflict = (conflicts.Count > 0),
			Conflicts = conflicts
		};
	}
}
