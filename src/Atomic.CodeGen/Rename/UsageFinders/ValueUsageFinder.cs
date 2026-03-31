using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Atomic.CodeGen.Rename.Models;

namespace Atomic.CodeGen.Rename.UsageFinders;

public sealed class ValueUsageFinder : IUsageFinder
{
	public RenameType Type => RenameType.Value;

	public List<UsageMatch> FindUsages(RenameContext context, IEnumerable<string> files, ApiRegistry registry, ImportAnalyzer importAnalyzer)
	{
		List<UsageMatch> list = new List<UsageMatch>();
		string oldName = context.OldName;
		string newName = context.NewName;
		ApiEntry byClassName = registry.GetByClassName(context.OwnerName);
		if (byClassName == null)
		{
			context.Errors.Add("Could not find API '" + context.OwnerName + "' in registry");
			return list;
		}
		(string, string, string)[] array = new(string, string, string)[7]
		{
			("\\bGet" + Regex.Escape(oldName) + "\\b", "Get" + newName, "GetMethod"),
			("\\bSet" + Regex.Escape(oldName) + "\\b", "Set" + newName, "SetMethod"),
			("\\bHas" + Regex.Escape(oldName) + "\\b", "Has" + newName, "HasMethod"),
			("\\bDel" + Regex.Escape(oldName) + "\\b", "Del" + newName, "DelMethod"),
			("\\bAdd" + Regex.Escape(oldName) + "\\b", "Add" + newName, "AddMethod"),
			("\\bTryGet" + Regex.Escape(oldName) + "\\b", "TryGet" + newName, "TryGetMethod"),
			("\\bRef" + Regex.Escape(oldName) + "\\b", "Ref" + newName, "RefMethod")
		};
		string pattern = $"\\b({Regex.Escape(context.OwnerName)}|Values)\\.{Regex.Escape(oldName)}\\b";
		foreach (string file in files)
		{
			if (!File.Exists(file))
			{
				continue;
			}
			string[] array2 = File.ReadAllText(file).Split('\n');
			FileImports imports = importAnalyzer.GetImports(file);
			List<ApiEntry> list2 = (from a in registry.GetApisWithValue(oldName)
				where imports.HasNamespaceImport(a.Namespace)
				select a).ToList();
			(string, string, string)[] array3 = array;
			string[] array4;
			for (int num = 0; num < array3.Length; num++)
			{
				(string, string, string) tuple = array3[num];
				string item = tuple.Item1;
				string item2 = tuple.Item2;
				string item3 = tuple.Item3;
				Regex regex = new Regex(item);
				int num2 = 0;
				array4 = array2;
				foreach (string text in array4)
				{
					num2++;
					foreach (Match item4 in regex.Matches(text))
					{
						bool flag = list2.Count > 1;
						list.Add(new UsageMatch
						{
							FilePath = file,
							Line = num2,
							Column = item4.Index + 1,
							Length = item4.Length,
							MatchedText = item4.Value,
							ReplacementText = item2,
							LineContext = text.TrimEnd('\r'),
							Category = item3,
							IsAmbiguous = flag,
							PossibleApis = (flag ? list2.Select((ApiEntry a) => a.ClassName).ToList() : null)
						});
					}
				}
			}
			Regex regex2 = new Regex(pattern);
			int num4 = 0;
			array4 = array2;
			foreach (string text2 in array4)
			{
				num4++;
				foreach (Match item5 in regex2.Matches(text2))
				{
					string replacementText = item5.Groups[1].Value + "." + newName;
					list.Add(new UsageMatch
					{
						FilePath = file,
						Line = num4,
						Column = item5.Index + 1,
						Length = item5.Length,
						MatchedText = item5.Value,
						ReplacementText = replacementText,
						LineContext = text2.TrimEnd('\r'),
						Category = "DirectReference",
						IsAmbiguous = false
					});
				}
			}
			Regex regex3 = new Regex($"\\b{Regex.Escape(byClassName.Namespace)}\\.{Regex.Escape(context.OwnerName)}\\.{Regex.Escape(oldName)}\\b");
			int num5 = 0;
			array4 = array2;
			foreach (string text3 in array4)
			{
				num5++;
				foreach (Match item6 in regex3.Matches(text3))
				{
					list.Add(new UsageMatch
					{
						FilePath = file,
						Line = num5,
						Column = item6.Index + 1,
						Length = item6.Length,
						MatchedText = item6.Value,
						ReplacementText = $"{byClassName.Namespace}.{context.OwnerName}.{newName}",
						LineContext = text3.TrimEnd('\r'),
						Category = "FullyQualifiedReference",
						IsAmbiguous = false
					});
				}
			}
		}
		return list;
	}
}
