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
		List<UsageMatch> results = new List<UsageMatch>();
		string oldName = context.OldName;
		string newName = context.NewName;
		string oldBaseName = (oldName.EndsWith("Behaviour", StringComparison.OrdinalIgnoreCase) ? oldName.Substring(0, oldName.Length - "Behaviour".Length) : oldName);
		string newBaseName = (newName.EndsWith("Behaviour", StringComparison.OrdinalIgnoreCase) ? newName.Substring(0, newName.Length - "Behaviour".Length) : newName);
		string oldBehaviourName = oldBaseName + "Behaviour";
		string newBehaviourName = newBaseName + "Behaviour";
		(string, string, string)[] array = new(string, string, string)[5]
		{
			("\\bHas" + Regex.Escape(oldBehaviourName) + "\\b", "Has" + newBehaviourName, "HasMethod"),
			("\\bGet" + Regex.Escape(oldBehaviourName) + "\\b", "Get" + newBehaviourName, "GetMethod"),
			("\\bAdd" + Regex.Escape(oldBehaviourName) + "\\b", "Add" + newBehaviourName, "AddMethod"),
			("\\bDel" + Regex.Escape(oldBehaviourName) + "\\b", "Del" + newBehaviourName, "DelMethod"),
			("\\bTryGet" + Regex.Escape(oldBehaviourName) + "\\b", "TryGet" + newBehaviourName, "TryGetMethod")
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
			List<ApiEntry> accessibleApis = (from a in registry.GetApisWithBehaviour(oldName)
				where imports.HasNamespaceImport(a.Namespace)
				select a).ToList();
			(string, string, string)[] array4 = array;
			string[] array5;
			for (int i = 0; i < array4.Length; i++)
			{
				(string, string, string) tuple = array4[i];
				string methodPattern = tuple.Item1;
				string methodReplacement = tuple.Item2;
				string methodCategory = tuple.Item3;
				Regex regex = new Regex(methodPattern);
				int lineNumber = 0;
				array5 = array3;
				foreach (string currentLine in array5)
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
			array4 = array2;
			for (int i = 0; i < array4.Length; i++)
			{
				(string, string, string) tuple2 = array4[i];
				string typePattern = tuple2.Item1;
				string typeReplacement = tuple2.Item2;
				string typeCategory = tuple2.Item3;
				Regex regex2 = new Regex(typePattern);
				int typeLineNumber = 0;
				array5 = array3;
				foreach (string currentLine in array5)
				{
					typeLineNumber++;
					foreach (Match regexMatch in regex2.Matches(currentLine))
					{
						string replacementText = regex2.Replace(regexMatch.Value, typeReplacement);
						if (typeCategory == "VariableDecl")
						{
							replacementText = newName;
							int length = oldName.Length;
							results.Add(new UsageMatch
							{
								FilePath = file,
								Line = typeLineNumber,
								Column = regexMatch.Index + 1,
								Length = length,
								MatchedText = oldName,
								ReplacementText = replacementText,
								LineContext = currentLine.TrimEnd('\r'),
								Category = typeCategory,
								IsAmbiguous = false
							});
						}
						else
						{
							results.Add(new UsageMatch
							{
								FilePath = file,
								Line = typeLineNumber,
								Column = regexMatch.Index + 1,
								Length = regexMatch.Length,
								MatchedText = regexMatch.Value,
								ReplacementText = replacementText,
								LineContext = currentLine.TrimEnd('\r'),
								Category = typeCategory,
								IsAmbiguous = false
							});
						}
					}
				}
			}
			Regex regex3 = new Regex("\\bclass\\s+" + Regex.Escape(oldName) + "\\b");
			int classLineNumber = 0;
			array5 = array3;
			foreach (string currentLine in array5)
			{
				classLineNumber++;
				foreach (Match classMatch in regex3.Matches(currentLine))
				{
					results.Add(new UsageMatch
					{
						FilePath = file,
						Line = classLineNumber,
						Column = classMatch.Index + 1,
						Length = classMatch.Length,
						MatchedText = classMatch.Value,
						ReplacementText = "class " + newName,
						LineContext = currentLine.TrimEnd('\r'),
						Category = "ClassDeclaration",
						IsAmbiguous = false
					});
				}
			}
		}
		return results;
	}
}
