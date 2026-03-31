using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Atomic.CodeGen.Rename.Models;

namespace Atomic.CodeGen.Rename.UsageFinders;

public sealed class BehaviourUsageFinder : IUsageFinder
{
	public RenameType Type => RenameType.Behaviour;

	public List<UsageMatch> FindUsages(RenameContext context, IEnumerable<string> files, ApiRegistry registry, ImportAnalyzer importAnalyzer)
	{
		List<UsageMatch> list = new List<UsageMatch>();
		string oldName = context.OldName;
		string newName = context.NewName;
		string text = (oldName.EndsWith("Behaviour", StringComparison.OrdinalIgnoreCase) ? oldName.Substring(0, oldName.Length - "Behaviour".Length) : oldName);
		string obj = (newName.EndsWith("Behaviour", StringComparison.OrdinalIgnoreCase) ? newName.Substring(0, newName.Length - "Behaviour".Length) : newName);
		string str = text + "Behaviour";
		string text2 = obj + "Behaviour";
		(string, string, string)[] array = new(string, string, string)[5]
		{
			("\\bHas" + Regex.Escape(str) + "\\b", "Has" + text2, "HasMethod"),
			("\\bGet" + Regex.Escape(str) + "\\b", "Get" + text2, "GetMethod"),
			("\\bAdd" + Regex.Escape(str) + "\\b", "Add" + text2, "AddMethod"),
			("\\bDel" + Regex.Escape(str) + "\\b", "Del" + text2, "DelMethod"),
			("\\bTryGet" + Regex.Escape(str) + "\\b", "TryGet" + text2, "TryGetMethod")
		};
		(string, string, string)[] array2 = new(string, string, string)[8]
		{
			("\\bnew\\s+" + Regex.Escape(oldName) + "\\s*\\(", "new " + newName + "(", "Constructor"),
			("\\btypeof\\s*\\(\\s*" + Regex.Escape(oldName) + "\\s*\\)", "typeof(" + newName + ")", "TypeOf"),
			("\\bnameof\\s*\\(\\s*" + Regex.Escape(oldName) + "\\s*\\)", "nameof(" + newName + ")", "NameOf"),
			("\\b(is|as)\\s+" + Regex.Escape(oldName) + "\\b", "$1 " + newName, "TypeCheck"),
			("<\\s*" + Regex.Escape(oldName) + "\\s*>", "<" + newName + ">", "GenericArg"),
			("\\b" + Regex.Escape(oldName) + "\\s*\\??\\s+(\\w+)\\s*[=;,)]", newName + "$0", "VariableDecl"),
			("\\(\\s*" + Regex.Escape(oldName) + "\\s*\\)", "(" + newName + ")", "Cast"),
			("(public|private|protected|internal|static)\\s+" + Regex.Escape(oldName) + "\\b", "$1 " + newName, "ReturnType")
		};
		foreach (string file in files)
		{
			if (!File.Exists(file))
			{
				continue;
			}
			string[] array3 = File.ReadAllText(file).Split('\n');
			FileImports imports = importAnalyzer.GetImports(file);
			List<ApiEntry> list2 = (from a in registry.GetApisWithBehaviour(oldName)
				where imports.HasNamespaceImport(a.Namespace)
				select a).ToList();
			(string, string, string)[] array4 = array;
			string[] array5;
			for (int num = 0; num < array4.Length; num++)
			{
				(string, string, string) tuple = array4[num];
				string item = tuple.Item1;
				string item2 = tuple.Item2;
				string item3 = tuple.Item3;
				Regex regex = new Regex(item);
				int num2 = 0;
				array5 = array3;
				foreach (string text3 in array5)
				{
					num2++;
					foreach (Match item7 in regex.Matches(text3))
					{
						bool flag = list2.Count > 1;
						list.Add(new UsageMatch
						{
							FilePath = file,
							Line = num2,
							Column = item7.Index + 1,
							Length = item7.Length,
							MatchedText = item7.Value,
							ReplacementText = item2,
							LineContext = text3.TrimEnd('\r'),
							Category = item3,
							IsAmbiguous = flag,
							PossibleApis = (flag ? list2.Select((ApiEntry a) => a.ClassName).ToList() : null)
						});
					}
				}
			}
			array4 = array2;
			for (int num = 0; num < array4.Length; num++)
			{
				(string, string, string) tuple2 = array4[num];
				string item4 = tuple2.Item1;
				string item5 = tuple2.Item2;
				string item6 = tuple2.Item3;
				Regex regex2 = new Regex(item4);
				int num4 = 0;
				array5 = array3;
				foreach (string text4 in array5)
				{
					num4++;
					foreach (Match item8 in regex2.Matches(text4))
					{
						string replacementText = regex2.Replace(item8.Value, item5);
						if (item6 == "VariableDecl")
						{
							replacementText = newName;
							int length = oldName.Length;
							list.Add(new UsageMatch
							{
								FilePath = file,
								Line = num4,
								Column = item8.Index + 1,
								Length = length,
								MatchedText = oldName,
								ReplacementText = replacementText,
								LineContext = text4.TrimEnd('\r'),
								Category = item6,
								IsAmbiguous = false
							});
						}
						else
						{
							list.Add(new UsageMatch
							{
								FilePath = file,
								Line = num4,
								Column = item8.Index + 1,
								Length = item8.Length,
								MatchedText = item8.Value,
								ReplacementText = replacementText,
								LineContext = text4.TrimEnd('\r'),
								Category = item6,
								IsAmbiguous = false
							});
						}
					}
				}
			}
			Regex regex3 = new Regex("\\bclass\\s+" + Regex.Escape(oldName) + "\\b");
			int num5 = 0;
			array5 = array3;
			foreach (string text5 in array5)
			{
				num5++;
				foreach (Match item9 in regex3.Matches(text5))
				{
					list.Add(new UsageMatch
					{
						FilePath = file,
						Line = num5,
						Column = item9.Index + 1,
						Length = item9.Length,
						MatchedText = item9.Value,
						ReplacementText = "class " + newName,
						LineContext = text5.TrimEnd('\r'),
						Category = "ClassDeclaration",
						IsAmbiguous = false
					});
				}
			}
		}
		return list;
	}
}
