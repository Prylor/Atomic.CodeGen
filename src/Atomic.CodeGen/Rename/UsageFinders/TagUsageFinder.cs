using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Atomic.CodeGen.Rename.Models;

namespace Atomic.CodeGen.Rename.UsageFinders;

public sealed class TagUsageFinder : IUsageFinder
{
	public RenameType Type => RenameType.Tag;

	public List<UsageMatch> FindUsages(RenameContext context, IEnumerable<string> files, ApiRegistry registry, ImportAnalyzer importAnalyzer)
	{
		List<UsageMatch> results = new List<UsageMatch>();
		string oldName = context.OldName;
		string newName = context.NewName;
		ApiEntry ownerApi = registry.GetByClassName(context.OwnerName);
		if (ownerApi == null)
		{
			context.Errors.Add("Could not find API '" + context.OwnerName + "' in registry");
			return results;
		}
		(string, string, string)[] tagMethodPatterns = new(string, string, string)[3]
		{
			("\\bHas" + Regex.Escape(oldName) + "Tag\\b", "Has" + newName + "Tag", "MethodCall"),
			("\\bAdd" + Regex.Escape(oldName) + "Tag\\b", "Add" + newName + "Tag", "MethodCall"),
			("\\bDel" + Regex.Escape(oldName) + "Tag\\b", "Del" + newName + "Tag", "MethodCall")
		};
		string pattern = $"\\b({Regex.Escape(context.OwnerName)}|Tags)\\.{Regex.Escape(oldName)}\\b";
		foreach (string file in files)
		{
			if (!File.Exists(file))
			{
				continue;
			}
			string[] sourceLines = File.ReadAllText(file).Split('\n');
			FileImports imports = importAnalyzer.GetImports(file);
			imports.HasNamespaceImport(ownerApi.Namespace);
			List<ApiEntry> accessibleApis = (from a in registry.GetApisWithTag(oldName)
				where imports.HasNamespaceImport(a.Namespace)
				select a).ToList();
			for (int i = 0; i < tagMethodPatterns.Length; i++)
			{
				(string, string, string) tuple = tagMethodPatterns[i];
				string methodPattern = tuple.Item1;
				string methodReplacement = tuple.Item2;
				string methodCategory = tuple.Item3;
				Regex regex = new Regex(methodPattern);
				int lineNumber = 0;
				foreach (string currentLine in sourceLines)
				{
					lineNumber++;
					foreach (Match regexMatch in regex.Matches(currentLine))
					{
						bool isAmbiguous = accessibleApis.Count > 1;
						results.Add(new UsageMatch
						{
							FilePath = file,
							Line = lineNumber,
							Column = regexMatch.Index + 1,
							Length = regexMatch.Length,
							MatchedText = regexMatch.Value,
							ReplacementText = methodReplacement,
							LineContext = currentLine.TrimEnd('\r'),
							Category = methodCategory,
							IsAmbiguous = isAmbiguous,
							PossibleApis = (isAmbiguous ? accessibleApis.Select((ApiEntry a) => a.ClassName).ToList() : null)
						});
					}
				}
			}
			Regex regex2 = new Regex(pattern);
			int directRefLineNumber = 0;
			foreach (string currentLine in sourceLines)
			{
				directRefLineNumber++;
				foreach (Match directRefMatch in regex2.Matches(currentLine))
				{
					string replacementText = directRefMatch.Groups[1].Value + "." + newName;
					results.Add(new UsageMatch
					{
						FilePath = file,
						Line = directRefLineNumber,
						Column = directRefMatch.Index + 1,
						Length = directRefMatch.Length,
						MatchedText = directRefMatch.Value,
						ReplacementText = replacementText,
						LineContext = currentLine.TrimEnd('\r'),
						Category = "DirectReference",
						IsAmbiguous = false
					});
				}
			}
			Regex regex3 = new Regex($"\\b{Regex.Escape(ownerApi.Namespace)}\\.{Regex.Escape(context.OwnerName)}\\.{Regex.Escape(oldName)}\\b");
			int fqnLineNumber = 0;
			foreach (string currentLine in sourceLines)
			{
				fqnLineNumber++;
				foreach (Match fqnMatch in regex3.Matches(currentLine))
				{
					results.Add(new UsageMatch
					{
						FilePath = file,
						Line = fqnLineNumber,
						Column = fqnMatch.Index + 1,
						Length = fqnMatch.Length,
						MatchedText = fqnMatch.Value,
						ReplacementText = $"{ownerApi.Namespace}.{context.OwnerName}.{newName}",
						LineContext = currentLine.TrimEnd('\r'),
						Category = "FullyQualifiedReference",
						IsAmbiguous = false
					});
				}
			}
		}
		return results;
	}
}
