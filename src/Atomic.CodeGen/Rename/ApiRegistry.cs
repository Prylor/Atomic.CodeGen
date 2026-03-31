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
				if (!apiRegistry._apisByTag.TryGetValue(tag, out List<ApiEntry> value))
				{
					value = new List<ApiEntry>();
					apiRegistry._apisByTag[tag] = value;
				}
				value.Add(apiEntry);
			}
			foreach (string value4 in apiEntry.Values)
			{
				if (!apiRegistry._apisByValue.TryGetValue(value4, out List<ApiEntry> value2))
				{
					value2 = new List<ApiEntry>();
					apiRegistry._apisByValue[value4] = value2;
				}
				value2.Add(apiEntry);
			}
			foreach (string behaviour in apiEntry.Behaviours)
			{
				if (!apiRegistry._apisByBehaviour.TryGetValue(behaviour, out List<ApiEntry> value3))
				{
					value3 = new List<ApiEntry>();
					apiRegistry._apisByBehaviour[behaviour] = value3;
				}
				value3.Add(apiEntry);
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
		if (!_apisByTag.TryGetValue(tagName, out List<ApiEntry> value))
		{
			return new List<ApiEntry>();
		}
		return value;
	}

	public IReadOnlyList<ApiEntry> GetApisWithValue(string valueName)
	{
		if (!_apisByValue.TryGetValue(valueName, out List<ApiEntry> value))
		{
			return new List<ApiEntry>();
		}
		return value;
	}

	public IReadOnlyList<ApiEntry> GetApisWithBehaviour(string behaviourName)
	{
		if (!_apisByBehaviour.TryGetValue(behaviourName, out List<ApiEntry> value))
		{
			return new List<ApiEntry>();
		}
		return value;
	}

	public List<ApiEntry> GetAccessibleApis(IEnumerable<string> usingNamespaces)
	{
		HashSet<string> namespaceSet = usingNamespaces.ToHashSet<string>(StringComparer.OrdinalIgnoreCase);
		return _apisByClassName.Values.Where((ApiEntry api) => namespaceSet.Contains(api.Namespace)).ToList();
	}

	public AmbiguityResult CheckAmbiguity(RenameType type, string symbolName, IEnumerable<string> accessibleNamespaces)
	{
		List<ApiEntry> accessibleApis = GetAccessibleApis(accessibleNamespaces);
		List<ApiEntry> list = type switch
		{
			RenameType.Tag => accessibleApis.Where((ApiEntry a) => a.Tags.Contains(symbolName)).ToList(), 
			RenameType.Value => accessibleApis.Where((ApiEntry a) => a.Values.Contains(symbolName)).ToList(), 
			RenameType.Behaviour => accessibleApis.Where((ApiEntry a) => a.Behaviours.Contains(symbolName)).ToList(), 
			_ => new List<ApiEntry>(), 
		};
		return new AmbiguityResult
		{
			IsAmbiguous = (list.Count > 1),
			MatchingApis = list
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
		List<string> list = new List<string>();
		if (byClassName.Tags.Contains(newName))
		{
			list.Add("Tag '" + newName + "' already exists in " + apiClassName);
		}
		if (byClassName.Values.Contains(newName))
		{
			list.Add("Value '" + newName + "' already exists in " + apiClassName);
		}
		if (byClassName.Behaviours.Contains(newName))
		{
			list.Add("Behaviour '" + newName + "' already exists in " + apiClassName);
		}
		return new ConflictResult
		{
			HasConflict = (list.Count > 0),
			Conflicts = list
		};
	}
}
