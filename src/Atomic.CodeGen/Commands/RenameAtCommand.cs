using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Atomic.CodeGen.Core.Models;
using Atomic.CodeGen.Rename;
using Atomic.CodeGen.Rename.Models;
using Atomic.CodeGen.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Atomic.CodeGen.Commands;

public static class RenameAtCommand
{
	private class SymbolInfo
	{
		public required RenameType Type { get; init; }

		public required string Name { get; init; }

		public required string OwnerName { get; init; }
	}

	private static readonly Regex PreprocessorSymbolRegex = new Regex("#(?:if|elif)\\s+(!?\\s*)?(\\w+)", RegexOptions.Multiline | RegexOptions.Compiled);

	public static Command Create()
	{
		Option<string> option = new Option<string>(new string[2] { "--project", "-p" }, () => Directory.GetCurrentDirectory(), "Path to project root");
		Option<string> option2 = new Option<string>(new string[2] { "--file", "-f" }, "Path to the source file")
		{
			IsRequired = true
		};
		Option<int> option3 = new Option<int>(new string[2] { "--line", "-l" }, "1-based line number")
		{
			IsRequired = true
		};
		Option<int> option4 = new Option<int>(new string[2] { "--column", "-c" }, "1-based column number")
		{
			IsRequired = true
		};
		Option<string> option5 = new Option<string>(new string[1] { "--to" }, "New name for the symbol")
		{
			IsRequired = true
		};
		Option<bool> option6 = new Option<bool>(new string[2] { "--dry-run", "-d" }, () => false, "Preview changes without applying them");
		Option<bool> option7 = new Option<bool>(new string[2] { "--verbose", "-v" }, () => false, "Enable verbose logging");
		Option<bool> option8 = new Option<bool>(new string[1] { "--json" }, () => false, "Output results as JSON (for IDE integration)");
		Command command = new Command("rename-at", "Rename symbol at cursor position (for IDE integration)") { option, option2, option3, option4, option5, option6, option7, option8 };
		command.SetHandler(async delegate(string projectPath, string file, int line, int column, string to, bool dryRun, bool verbose, bool json)
		{
			Logger.SetVerbose(verbose);
			if (!json)
			{
				Logger.LogHeader("Atomic CodeGen - Rename At Cursor");
			}
			string filePath = (Path.IsPathRooted(file) ? file : Path.Combine(projectPath, file));
			if (!File.Exists(filePath))
			{
				OutputError("File not found: " + filePath, json);
			}
			else
			{
				SymbolInfo symbolInfo = await LocateSymbolAsync(filePath, line, column);
				if (symbolInfo == null)
				{
					OutputError($"No renameable symbol found at {Path.GetFileName(filePath)}:{line}:{column}", json);
				}
				else
				{
					if (!json)
					{
						Logger.LogInfo($"Found {symbolInfo.Type}: {symbolInfo.Name} in {symbolInfo.OwnerName}");
					}
					CodeGenConfig codeGenConfig = await ConfigLoader.LoadAsync(projectPath);
					RenameOrchestrator orchestrator = new RenameOrchestrator(codeGenConfig.GetAbsoluteProjectRoot(), codeGenConfig);
					RenameContext context = await orchestrator.CreateContextAsync(symbolInfo.Type, symbolInfo.Name, to, symbolInfo.OwnerName, null, symbolInfo.Type == RenameType.Behaviour);
					if (!context.IsValid)
					{
						OutputErrors(context.Errors, json);
					}
					else
					{
						context = await orchestrator.FindUsagesAsync(context);
						if (!context.IsValid)
						{
							OutputErrors(context.Errors, json);
						}
						else if (json)
						{
							OutputJsonResult(context, dryRun);
						}
						else
						{
							List<FileChangeSummary> preview = orchestrator.GetPreview(context);
							ConsoleUI.ShowPreview(context, preview);
							if (dryRun)
							{
								Logger.LogInfo("Dry run complete - no changes made");
							}
							else if (orchestrator.Execute(context))
							{
								await orchestrator.RegenerateAffectedApiAsync(context);
								ConsoleUI.ShowSuccess(context);
							}
							else
							{
								Logger.LogError("Rename failed - changes have been rolled back");
							}
						}
					}
				}
			}
		}, option, option2, option3, option4, option5, option6, option7, option8);
		return command;
	}

	private static async Task<SymbolInfo?> LocateSymbolAsync(string filePath, int line, int column)
	{
		string sourceCode = await File.ReadAllTextAsync(filePath);
		CSharpParseOptions options = CreateParseOptionsWithSymbols(sourceCode);
		SyntaxNode syntaxNode = await CSharpSyntaxTree.ParseText(sourceCode, options).GetRootAsync();
		string[] sourceLines = sourceCode.Split('\n');
		int absoluteOffset = 0;
		for (int i = 0; i < line - 1 && i < sourceLines.Length; i++)
		{
			absoluteOffset += sourceLines[i].Length + 1;
		}
		absoluteOffset += column - 1;
		SyntaxToken syntaxToken = syntaxNode.FindToken(absoluteOffset);
		if (syntaxToken == default(SyntaxToken))
		{
			return null;
		}
		for (SyntaxNode parent = syntaxToken.Parent; parent != null; parent = parent.Parent)
		{
			if (parent is EnumMemberDeclarationSyntax enumMemberDeclarationSyntax)
			{
				EnumDeclarationSyntax enumDeclarationSyntax = enumMemberDeclarationSyntax.Parent as EnumDeclarationSyntax;
				if (enumDeclarationSyntax?.Identifier.Text == "Tags")
				{
					ClassDeclarationSyntax classDeclarationSyntax = FindOwnerClass(enumDeclarationSyntax);
					if (classDeclarationSyntax != null)
					{
						return new SymbolInfo
						{
							Type = RenameType.Tag,
							Name = enumMemberDeclarationSyntax.Identifier.Text,
							OwnerName = classDeclarationSyntax.Identifier.Text
						};
					}
				}
			}
			if (parent is FieldDeclarationSyntax fieldDeclarationSyntax)
			{
				ClassDeclarationSyntax classDeclarationSyntax2 = fieldDeclarationSyntax.Parent as ClassDeclarationSyntax;
				if (classDeclarationSyntax2?.Identifier.Text == "Values")
				{
					ClassDeclarationSyntax classDeclarationSyntax3 = FindOwnerClass(classDeclarationSyntax2);
					if (classDeclarationSyntax3 != null)
					{
						string fieldName = fieldDeclarationSyntax.Declaration.Variables.FirstOrDefault()?.Identifier.Text;
						if (fieldName != null)
						{
							return new SymbolInfo
							{
								Type = RenameType.Value,
								Name = fieldName,
								OwnerName = classDeclarationSyntax3.Identifier.Text
							};
						}
					}
				}
			}
			if (parent is ClassDeclarationSyntax classDeclarationSyntax4 && classDeclarationSyntax4.AttributeLists.SelectMany((AttributeListSyntax al) => al.Attributes).Any((AttributeSyntax a) => a.Name.ToString().Contains("LinkTo")))
			{
				string linkedApiName = ExtractApiNameFromLinkTo(classDeclarationSyntax4.AttributeLists.SelectMany((AttributeListSyntax al) => al.Attributes).FirstOrDefault((AttributeSyntax a) => a.Name.ToString().Contains("LinkTo")));
				if (linkedApiName != null)
				{
					return new SymbolInfo
					{
						Type = RenameType.Behaviour,
						Name = classDeclarationSyntax4.Identifier.Text,
						OwnerName = linkedApiName
					};
				}
			}
			if (parent is PropertyDeclarationSyntax { Identifier: { Text: "EntityName" }, ExpressionBody: not null } propertyDeclarationSyntax && propertyDeclarationSyntax.ExpressionBody.Expression is LiteralExpressionSyntax { Token: { Value: string value } } && propertyDeclarationSyntax.Parent is ClassDeclarationSyntax classDeclarationSyntax5)
			{
				return new SymbolInfo
				{
					Type = RenameType.Domain,
					Name = value,
					OwnerName = classDeclarationSyntax5.Identifier.Text
				};
			}
			if (parent is InvocationExpressionSyntax invocation)
			{
				string methodName = GetMethodName(invocation);
				if (methodName != null)
				{
					string[] valuePrefixes = new string[7] { "Get", "Set", "Has", "Del", "Add", "TryGet", "Ref" };
					foreach (string prefix in valuePrefixes)
					{
						if (methodName.StartsWith(prefix) && methodName.Length > prefix.Length)
						{
							methodName.Substring(prefix.Length);
							break;
						}
					}
					if (methodName.EndsWith("Tag"))
					{
						string[] tagPrefixes = new string[3] { "Has", "Add", "Del" };
						foreach (string prefix in tagPrefixes)
						{
							if (methodName.StartsWith(prefix) && methodName.EndsWith("Tag"))
							{
								methodName.Substring(prefix.Length, methodName.Length - prefix.Length - 3);
								break;
							}
						}
					}
				}
			}
		}
		return null;
	}

	private static ClassDeclarationSyntax? FindOwnerClass(SyntaxNode node)
	{
		for (SyntaxNode parent = node.Parent; parent != null; parent = parent.Parent)
		{
			if (parent is ClassDeclarationSyntax classDeclarationSyntax && classDeclarationSyntax.AttributeLists.SelectMany((AttributeListSyntax al) => al.Attributes).Any((AttributeSyntax a) => a.Name.ToString().Contains("EntityAPI")))
			{
				return classDeclarationSyntax;
			}
		}
		return null;
	}

	private static string? ExtractApiNameFromLinkTo(AttributeSyntax? attr)
	{
		if (attr != null && attr.ArgumentList?.Arguments.Count > 0 && attr.ArgumentList.Arguments[0].Expression is TypeOfExpressionSyntax typeOfExpressionSyntax)
		{
			return typeOfExpressionSyntax.Type.ToString().Split('.').Last();
		}
		return null;
	}

	private static string? GetMethodName(InvocationExpressionSyntax invocation)
	{
		ExpressionSyntax expression = invocation.Expression;
		if (!(expression is MemberAccessExpressionSyntax memberAccessExpressionSyntax))
		{
			if (expression is IdentifierNameSyntax { Identifier: var identifier })
			{
				return identifier.Text;
			}
			return null;
		}
		return memberAccessExpressionSyntax.Name.Identifier.Text;
	}

	private static void OutputError(string message, bool json)
	{
		if (json)
		{
			Console.WriteLine("{\"success\": false, \"error\": \"" + EscapeJson(message) + "\"}");
		}
		else
		{
			Logger.LogError(message);
		}
	}

	private static void OutputErrors(List<string> errors, bool json)
	{
		if (json)
		{
			string errorsJson = string.Join(", ", errors.Select((string e) => "\"" + EscapeJson(e) + "\""));
			Console.WriteLine("{\"success\": false, \"errors\": [" + errorsJson + "]}");
			return;
		}
		foreach (string error in errors)
		{
			Logger.LogError(error);
		}
	}

	private static void OutputJsonResult(RenameContext context, bool dryRun)
	{
		context.CertainUsages.Select((UsageMatch u) => new
		{
			file = u.FilePath,
			line = u.Line,
			column = u.Column,
			oldText = u.MatchedText,
			newText = u.ReplacementText
		});
		Console.WriteLine($"{{\r\n  \"success\": true,\r\n  \"dryRun\": {dryRun.ToString().ToLower()},\r\n  \"oldName\": \"{EscapeJson(context.OldName)}\",\r\n  \"newName\": \"{EscapeJson(context.NewName)}\",\r\n  \"type\": \"{context.Type}\",\r\n  \"usageCount\": {context.CertainUsages.Count},\r\n  \"ambiguousCount\": {context.AmbiguousUsages.Count},\r\n  \"fileCount\": {context.AffectedFiles.Count()}\r\n}}");
	}

	private static string EscapeJson(string s)
	{
		return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
	}

	private static CSharpParseOptions CreateParseOptionsWithSymbols(string sourceCode)
	{
		HashSet<string> preprocessorSymbols = new HashSet<string>();
		foreach (Match match in PreprocessorSymbolRegex.Matches(sourceCode))
		{
			string symbolName = match.Groups[2].Value;
			if (!string.IsNullOrEmpty(symbolName))
			{
				preprocessorSymbols.Add(symbolName);
			}
		}
		return new CSharpParseOptions(LanguageVersion.Default, DocumentationMode.Parse, SourceCodeKind.Regular, preprocessorSymbols);
	}
}
