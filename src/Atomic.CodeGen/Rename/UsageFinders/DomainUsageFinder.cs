using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Atomic.CodeGen.Rename.Models;

namespace Atomic.CodeGen.Rename.UsageFinders;

public sealed class DomainUsageFinder : IUsageFinder
{
	private enum FileType
	{
		Unrelated,
		DomainDefinition,
		GeneratedByDomain
	}

	private static readonly Regex SourceFilePathRegex = new Regex("^\\s*\\*\\s*Source file path:\\s*(.+?)\\s*$", RegexOptions.Multiline | RegexOptions.Compiled);

	private static readonly Regex TypeDefinitionRegex = new Regex("(?:public|internal|private|protected)\\s+(?:(?:sealed|abstract|static|partial|readonly|new)\\s+)*(?:class|interface|struct|record)\\s+(\\w+)", RegexOptions.Compiled);

	public RenameType Type => RenameType.Domain;

	public List<UsageMatch> FindUsages(RenameContext context, IEnumerable<string> files, ApiRegistry registry, ImportAnalyzer importAnalyzer)
	{
		List<UsageMatch> list = new List<UsageMatch>();
		string oldName = context.OldName;
		string newName = context.NewName;
		string sourceFilePath = context.SourceFilePath;
		_ = context.OwnerNamespace;
		List<string> list2 = files.ToList();
		Path.GetDirectoryName(Path.GetFullPath(sourceFilePath));
		string outputDirectory = context.OutputDirectory;
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		HashSet<string> hashSet2 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		foreach (string item2 in list2)
		{
			if (File.Exists(item2))
			{
				string content = File.ReadAllText(item2);
				switch (ClassifyFile(item2, content, sourceFilePath))
				{
				case FileType.DomainDefinition:
				{
					string fullPath2 = Path.GetFullPath(item2);
					hashSet.Add(fullPath2);
					hashSet2.Add(Path.GetDirectoryName(fullPath2) ?? "");
					FindEntityNameProperty(item2, content, oldName, newName, list);
					break;
				}
				case FileType.GeneratedByDomain:
				{
					string fullPath = Path.GetFullPath(item2);
					hashSet.Add(fullPath);
					hashSet2.Add(Path.GetDirectoryName(fullPath) ?? "");
					ExtractTypeDefinitions(content, oldName, newName, dictionary);
					break;
				}
				}
			}
		}
		if (dictionary.Count == 0)
		{
			AddExpectedTypes(oldName, newName, dictionary);
		}
		if (!string.IsNullOrEmpty(outputDirectory))
		{
			hashSet2.Add(Path.GetFullPath(outputDirectory));
		}
		string value;
		string key;
		foreach (string item3 in hashSet)
		{
			if (!File.Exists(item3))
			{
				continue;
			}
			string[] lines = File.ReadAllText(item3).Split('\n');
			foreach (KeyValuePair<string, string> item4 in dictionary)
			{
				item4.Deconstruct(out value, out key);
				string typeName = value;
				string newTypeName = key;
				string typeCategory = GetTypeCategory(typeName, oldName);
				FindTypeReferences(item3, lines, typeName, newTypeName, typeCategory, list);
			}
			string fileName = Path.GetFileName(item3);
			string text = Path.GetFullPath(item3).Replace('/', '\\');
			string value2 = Path.GetFullPath(sourceFilePath).Replace('/', '\\');
			if (fileName.Contains(oldName) && !text.Equals(value2, StringComparison.OrdinalIgnoreCase))
			{
				string path = fileName.Replace(oldName, newName);
				string item = Path.Combine(Path.GetDirectoryName(item3) ?? "", path);
				context.FileRenames.Add((item3, item));
			}
		}
		foreach (string item5 in FindConsumerFilesInDomainTree(list2, oldName, hashSet, hashSet2, dictionary.Keys))
		{
			if (!File.Exists(item5))
			{
				continue;
			}
			string[] lines2 = File.ReadAllText(item5).Split('\n');
			foreach (KeyValuePair<string, string> item6 in dictionary)
			{
				item6.Deconstruct(out key, out value);
				string typeName2 = key;
				string newTypeName2 = value;
				string category = GetTypeCategory(typeName2, oldName) + " (consumer)";
				FindTypeReferences(item5, lines2, typeName2, newTypeName2, category, list);
			}
		}
		return list;
	}

	private HashSet<string> FindConsumerFilesInDomainTree(List<string> files, string entityName, HashSet<string> generatedFiles, HashSet<string> generatedDirs, IEnumerable<string> definedTypeNames)
	{
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (string file in files)
		{
			string fullPath = Path.GetFullPath(file);
			if (generatedFiles.Contains(fullPath) || !File.Exists(file))
			{
				continue;
			}
			string fileDir = Path.GetDirectoryName(fullPath) ?? "";
			if (generatedDirs.Any((string genDir) => IsInDirectoryTree(fileDir, genDir)))
			{
				string content = File.ReadAllText(file);
				if (definedTypeNames.Any((string typeName) => Regex.IsMatch(content, "\\b" + Regex.Escape(typeName) + "\\b")))
				{
					hashSet.Add(fullPath);
				}
			}
		}
		return hashSet;
	}

	private bool IsInDirectoryTree(string childDir, string parentDir)
	{
		if (string.IsNullOrEmpty(parentDir))
		{
			return false;
		}
		string text = Path.GetFullPath(childDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		string value = Path.GetFullPath(parentDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		return text.StartsWith(value, StringComparison.OrdinalIgnoreCase);
	}

	private FileType ClassifyFile(string filePath, string content, string domainSourceFile)
	{
		string text = Path.GetFullPath(filePath).Replace('/', '\\');
		string text2 = Path.GetFullPath(domainSourceFile).Replace('/', '\\');
		if (text.Equals(text2, StringComparison.OrdinalIgnoreCase))
		{
			return FileType.DomainDefinition;
		}
		string input = string.Join("\n", content.Split('\n').Take(10));
		Match match = SourceFilePathRegex.Match(input);
		if (!match.Success)
		{
			return FileType.Unrelated;
		}
		string text3 = match.Groups[1].Value.Trim().Replace('/', '\\');
		if (!Path.IsPathRooted(text3))
		{
			string fileName = Path.GetFileName(text2);
			if (text3.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
			{
				return FileType.GeneratedByDomain;
			}
		}
		else
		{
			text3 = Path.GetFullPath(text3).Replace('/', '\\');
			if (text3.Equals(text2, StringComparison.OrdinalIgnoreCase))
			{
				return FileType.GeneratedByDomain;
			}
		}
		return FileType.Unrelated;
	}

	private void ExtractTypeDefinitions(string content, string entityName, string newEntityName, Dictionary<string, string> definedTypes)
	{
		foreach (Match item in TypeDefinitionRegex.Matches(content))
		{
			string value = item.Groups[1].Value;
			if (value.Contains(entityName, StringComparison.Ordinal))
			{
				string value2 = value.Replace(entityName, newEntityName);
				definedTypes.TryAdd(value, value2);
			}
		}
	}

	private void AddExpectedTypes(string entityName, string newEntityName, Dictionary<string, string> definedTypes)
	{
		string[] array = new string[11]
		{
			"", "Behaviours", "Factory", "Pool", "View", "ViewPool", "ViewCatalog", "Baker", "Installer", "UI",
			"Aspect"
		};
		foreach (string text in array)
		{
			definedTypes.TryAdd(entityName + text, newEntityName + text);
			definedTypes.TryAdd("I" + entityName + text, "I" + newEntityName + text);
		}
		definedTypes.TryAdd("Scene" + entityName, "Scene" + newEntityName);
		definedTypes.TryAdd("Scene" + entityName + "World", "Scene" + newEntityName + "World");
		definedTypes.TryAdd("Scene" + entityName + "Proxy", "Scene" + newEntityName + "Proxy");
		array = new string[8] { "Init", "Dispose", "Enable", "Disable", "Tick", "FixedTick", "LateTick", "Gizmos" };
		foreach (string text2 in array)
		{
			definedTypes.TryAdd(entityName + text2, newEntityName + text2);
		}
	}

	private bool UsesDomainTypes(string content, string? domainNamespace, IEnumerable<string> typeNames)
	{
		if (!string.IsNullOrEmpty(domainNamespace))
		{
			bool hasDomainImport = Regex.IsMatch(content, "using\\s+" + Regex.Escape(domainNamespace) + "\\s*;");
			bool flag = Regex.IsMatch(content, "namespace\\s+" + Regex.Escape(domainNamespace) + "\\b");
			if (!hasDomainImport && !flag)
			{
				return false;
			}
		}
		foreach (string typeName in typeNames)
		{
			if (Regex.IsMatch(content, "\\b" + Regex.Escape(typeName) + "\\b"))
			{
				return true;
			}
		}
		return false;
	}

	private void FindEntityNameProperty(string filePath, string content, string entityName, string newEntityName, List<UsageMatch> results)
	{
		string[] array = content.Split('\n');
		Regex regex = new Regex("EntityName\\s*=>\\s*\"" + Regex.Escape(entityName) + "\"");
		int num = 0;
		string[] array2 = array;
		foreach (string text in array2)
		{
			num++;
			foreach (Match item in regex.Matches(text))
			{
				results.Add(new UsageMatch
				{
					FilePath = filePath,
					Line = num,
					Column = item.Index + 1,
					Length = item.Length,
					MatchedText = item.Value,
					ReplacementText = "EntityName => \"" + newEntityName + "\"",
					LineContext = text.TrimEnd('\r'),
					Category = "EntityNameProperty",
					IsAmbiguous = false
				});
			}
		}
	}

	private string GetTypeCategory(string typeName, string entityName)
	{
		if (typeName.StartsWith("I") && typeName.Length > 1 && char.IsUpper(typeName[1]))
		{
			return "Interface";
		}
		if (typeName.StartsWith("Scene"))
		{
			if (!typeName.Contains("World"))
			{
				if (!typeName.Contains("Proxy"))
				{
					return "SceneEntity";
				}
				return "SceneProxy";
			}
			return "SceneWorld";
		}
		if (typeName.EndsWith("Behaviours"))
		{
			return "Behaviours";
		}
		if (typeName.EndsWith("Factory"))
		{
			return "Factory";
		}
		if (typeName.EndsWith("Pool"))
		{
			return "Pool";
		}
		if (typeName.EndsWith("Installer"))
		{
			return "Installer";
		}
		if (typeName.EndsWith("Baker"))
		{
			return "Baker";
		}
		if (typeName.EndsWith("View"))
		{
			return "View";
		}
		if (typeName.EndsWith("Aspect"))
		{
			return "Aspect";
		}
		if (typeName == entityName)
		{
			return "EntityType";
		}
		return "Type";
	}

	private void FindTypeReferences(string filePath, string[] lines, string typeName, string newTypeName, string category, List<UsageMatch> results)
	{
		Regex regex = new Regex("\\b" + Regex.Escape(typeName) + "\\b");
		int num = 0;
		foreach (string text in lines)
		{
			num++;
			foreach (Match item in regex.Matches(text))
			{
				if (!IsInStringLiteral(text, item.Index))
				{
					results.Add(new UsageMatch
					{
						FilePath = filePath,
						Line = num,
						Column = item.Index + 1,
						Length = item.Length,
						MatchedText = item.Value,
						ReplacementText = newTypeName,
						LineContext = text.TrimEnd('\r'),
						Category = category,
						IsAmbiguous = false
					});
				}
			}
		}
	}

	private bool IsInStringLiteral(string line, int position)
	{
		int num = 0;
		for (int i = 0; i < position && i < line.Length; i++)
		{
			if (line[i] == '"' && (i == 0 || line[i - 1] != '\\'))
			{
				num++;
			}
		}
		return num % 2 == 1;
	}
}
