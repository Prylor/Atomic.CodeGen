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
		List<UsageMatch> results = new List<UsageMatch>();
		string oldName = context.OldName;
		string newName = context.NewName;
		string sourceFilePath = context.SourceFilePath;
		_ = context.OwnerNamespace;
		List<string> allFiles = files.ToList();
		Path.GetDirectoryName(Path.GetFullPath(sourceFilePath));
		string outputDirectory = context.OutputDirectory;
		HashSet<string> generatedFilePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		HashSet<string> generatedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		Dictionary<string, string> typeRenames = new Dictionary<string, string>();
		foreach (string filePath in allFiles)
		{
			if (File.Exists(filePath))
			{
				string content = File.ReadAllText(filePath);
				switch (ClassifyFile(filePath, content, sourceFilePath))
				{
				case FileType.DomainDefinition:
				{
					string fullPath2 = Path.GetFullPath(filePath);
					generatedFilePaths.Add(fullPath2);
					generatedDirectories.Add(Path.GetDirectoryName(fullPath2) ?? "");
					FindEntityNameProperty(filePath, content, oldName, newName, results);
					break;
				}
				case FileType.GeneratedByDomain:
				{
					string fullPath = Path.GetFullPath(filePath);
					generatedFilePaths.Add(fullPath);
					generatedDirectories.Add(Path.GetDirectoryName(fullPath) ?? "");
					ExtractTypeDefinitions(content, oldName, newName, typeRenames);
					break;
				}
				}
			}
		}
		if (typeRenames.Count == 0)
		{
			AddExpectedTypes(oldName, newName, typeRenames);
		}
		if (!string.IsNullOrEmpty(outputDirectory))
		{
			generatedDirectories.Add(Path.GetFullPath(outputDirectory));
		}
		string deconstructedKey;
		string deconstructedValue;
		foreach (string generatedFile in generatedFilePaths)
		{
			if (!File.Exists(generatedFile))
			{
				continue;
			}
			string[] lines = File.ReadAllText(generatedFile).Split('\n');
			foreach (KeyValuePair<string, string> typeRename in typeRenames)
			{
				typeRename.Deconstruct(out deconstructedKey, out deconstructedValue);
				string typeName = deconstructedKey;
				string newTypeName = deconstructedValue;
				string typeCategory = GetTypeCategory(typeName, oldName);
				FindTypeReferences(generatedFile, lines, typeName, newTypeName, typeCategory, results);
			}
			string fileName = Path.GetFileName(generatedFile);
			string normalizedFilePath = Path.GetFullPath(generatedFile).Replace('/', '\\');
			string normalizedSourcePath = Path.GetFullPath(sourceFilePath).Replace('/', '\\');
			if (fileName.Contains(oldName) && !normalizedFilePath.Equals(normalizedSourcePath, StringComparison.OrdinalIgnoreCase))
			{
				string renamedFileName = fileName.Replace(oldName, newName);
				string renamedFilePath = Path.Combine(Path.GetDirectoryName(generatedFile) ?? "", renamedFileName);
				context.FileRenames.Add((generatedFile, renamedFilePath));
			}
		}
		foreach (string consumerFile in FindConsumerFilesInDomainTree(allFiles, oldName, generatedFilePaths, generatedDirectories, typeRenames.Keys))
		{
			if (!File.Exists(consumerFile))
			{
				continue;
			}
			string[] lines2 = File.ReadAllText(consumerFile).Split('\n');
			foreach (KeyValuePair<string, string> typeRename in typeRenames)
			{
				typeRename.Deconstruct(out deconstructedKey, out deconstructedValue);
				string typeName2 = deconstructedKey;
				string newTypeName2 = deconstructedValue;
				string category = GetTypeCategory(typeName2, oldName) + " (consumer)";
				FindTypeReferences(consumerFile, lines2, typeName2, newTypeName2, category, results);
			}
		}
		return results;
	}

	private HashSet<string> FindConsumerFilesInDomainTree(List<string> files, string entityName, HashSet<string> generatedFiles, HashSet<string> generatedDirs, IEnumerable<string> definedTypeNames)
	{
		HashSet<string> consumerFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
					consumerFiles.Add(fullPath);
				}
			}
		}
		return consumerFiles;
	}

	private bool IsInDirectoryTree(string childDir, string parentDir)
	{
		if (string.IsNullOrEmpty(parentDir))
		{
			return false;
		}
		string normalizedChild = Path.GetFullPath(childDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		string normalizedParent = Path.GetFullPath(parentDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		return normalizedChild.StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase);
	}

	private FileType ClassifyFile(string filePath, string content, string domainSourceFile)
	{
		string normalizedFilePath = Path.GetFullPath(filePath).Replace('/', '\\');
		string normalizedDomainPath = Path.GetFullPath(domainSourceFile).Replace('/', '\\');
		if (normalizedFilePath.Equals(normalizedDomainPath, StringComparison.OrdinalIgnoreCase))
		{
			return FileType.DomainDefinition;
		}
		string input = string.Join("\n", content.Split('\n').Take(10));
		Match match = SourceFilePathRegex.Match(input);
		if (!match.Success)
		{
			return FileType.Unrelated;
		}
		string referencedSourcePath = match.Groups[1].Value.Trim().Replace('/', '\\');
		if (!Path.IsPathRooted(referencedSourcePath))
		{
			string fileName = Path.GetFileName(normalizedDomainPath);
			if (referencedSourcePath.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
			{
				return FileType.GeneratedByDomain;
			}
		}
		else
		{
			referencedSourcePath = Path.GetFullPath(referencedSourcePath).Replace('/', '\\');
			if (referencedSourcePath.Equals(normalizedDomainPath, StringComparison.OrdinalIgnoreCase))
			{
				return FileType.GeneratedByDomain;
			}
		}
		return FileType.Unrelated;
	}

	private void ExtractTypeDefinitions(string content, string entityName, string newEntityName, Dictionary<string, string> definedTypes)
	{
		foreach (Match typeMatch in TypeDefinitionRegex.Matches(content))
		{
			string typeName = typeMatch.Groups[1].Value;
			if (typeName.Contains(entityName, StringComparison.Ordinal))
			{
				string renamedTypeName = typeName.Replace(entityName, newEntityName);
				definedTypes.TryAdd(typeName, renamedTypeName);
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
		foreach (string suffix in array)
		{
			definedTypes.TryAdd(entityName + suffix, newEntityName + suffix);
			definedTypes.TryAdd("I" + entityName + suffix, "I" + newEntityName + suffix);
		}
		definedTypes.TryAdd("Scene" + entityName, "Scene" + newEntityName);
		definedTypes.TryAdd("Scene" + entityName + "World", "Scene" + newEntityName + "World");
		definedTypes.TryAdd("Scene" + entityName + "Proxy", "Scene" + newEntityName + "Proxy");
		array = new string[8] { "Init", "Dispose", "Enable", "Disable", "Tick", "FixedTick", "LateTick", "Gizmos" };
		foreach (string lifecycleSuffix in array)
		{
			definedTypes.TryAdd(entityName + lifecycleSuffix, newEntityName + lifecycleSuffix);
		}
	}

	private bool UsesDomainTypes(string content, string? domainNamespace, IEnumerable<string> typeNames)
	{
		if (!string.IsNullOrEmpty(domainNamespace))
		{
			bool hasDomainImport = Regex.IsMatch(content, "using\\s+" + Regex.Escape(domainNamespace) + "\\s*;");
			bool isInDomainNamespace = Regex.IsMatch(content, "namespace\\s+" + Regex.Escape(domainNamespace) + "\\b");
			if (!hasDomainImport && !isInDomainNamespace)
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
		int lineNumber = 0;
		string[] array2 = array;
		foreach (string line in array2)
		{
			lineNumber++;
			foreach (Match regexMatch in regex.Matches(line))
			{
				results.Add(new UsageMatch
				{
					FilePath = filePath,
					Line = lineNumber,
					Column = regexMatch.Index + 1,
					Length = regexMatch.Length,
					MatchedText = regexMatch.Value,
					ReplacementText = "EntityName => \"" + newEntityName + "\"",
					LineContext = line.TrimEnd('\r'),
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
		int lineNumber = 0;
		foreach (string line in lines)
		{
			lineNumber++;
			foreach (Match regexMatch in regex.Matches(line))
			{
				if (!IsInStringLiteral(line, regexMatch.Index))
				{
					results.Add(new UsageMatch
					{
						FilePath = filePath,
						Line = lineNumber,
						Column = regexMatch.Index + 1,
						Length = regexMatch.Length,
						MatchedText = regexMatch.Value,
						ReplacementText = newTypeName,
						LineContext = line.TrimEnd('\r'),
						Category = category,
						IsAmbiguous = false
					});
				}
			}
		}
	}

	private bool IsInStringLiteral(string line, int position)
	{
		int quoteCount = 0;
		for (int i = 0; i < position && i < line.Length; i++)
		{
			if (line[i] == '"' && (i == 0 || line[i - 1] != '\\'))
			{
				quoteCount++;
			}
		}
		return quoteCount % 2 == 1;
	}
}
