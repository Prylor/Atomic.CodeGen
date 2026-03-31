using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Atomic.CodeGen.Core.Models;
using Atomic.CodeGen.Rename.Models;
using Atomic.CodeGen.Roslyn;
using Atomic.CodeGen.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;

namespace Atomic.CodeGen.Rename.UsageFinders;

public sealed class SemanticUsageFinder
{
	private readonly string _solutionPath;

	private readonly AnalyzerMode _analyzerMode;

	private readonly IEnumerable<string>? _includedProjects;

	private SemanticWorkspace? _workspace;

	private static readonly Regex SourceFilePathRegex = new Regex("^\\s*\\*\\s*Source file path:\\s*(.+?)\\s*$", RegexOptions.Multiline | RegexOptions.Compiled);

	private static readonly Regex PreprocessorSymbolRegex = new Regex("#(?:if|elif)\\s+(!?\\s*)?(\\w+)", RegexOptions.Multiline | RegexOptions.Compiled);

	public SemanticUsageFinder(string solutionPath, AnalyzerMode analyzerMode = AnalyzerMode.Auto, IEnumerable<string>? includedProjects = null)
	{
		_solutionPath = solutionPath;
		_analyzerMode = analyzerMode;
		_includedProjects = includedProjects;
	}

	public async Task<List<UsageMatch>> FindUsagesAsync(RenameContext context)
	{
		_workspace = new SemanticWorkspace(_solutionPath, _analyzerMode, _includedProjects);
		try
		{
			if (!(await _workspace.LoadAsync()))
			{
				Logger.LogWarning("Failed to load solution for semantic analysis.");
				return new List<UsageMatch>();
			}
			return context.Type switch
			{
				RenameType.Tag => await FindTagUsagesAsync(context), 
				RenameType.Value => await FindValueUsagesAsync(context), 
				RenameType.Behaviour => await FindBehaviourUsagesAsync(context), 
				RenameType.Domain => await FindDomainUsagesAsync(context), 
				_ => new List<UsageMatch>(), 
			};
		}
		finally
		{
			_workspace?.Dispose();
			_workspace = null;
		}
	}

	private async Task<List<UsageMatch>> FindTagUsagesAsync(RenameContext context)
	{
		List<UsageMatch> results = new List<UsageMatch>();
		string tagName = context.OldName;
		string newTagName = context.NewName;
		INamedTypeSymbol apiSymbol = await FindApiClassAsync(context.OwnerName, context.OwnerNamespace);
		if (apiSymbol == null)
		{
			Logger.LogWarning("Could not find API class '" + context.OwnerName + "' for semantic analysis");
			return results;
		}
		IFieldSymbol fieldSymbol = apiSymbol.GetMembers(tagName).OfType<IFieldSymbol>().FirstOrDefault((IFieldSymbol f) => f.IsStatic);
		if (fieldSymbol != null)
		{
			foreach (SemanticUsage item in await FindSymbolReferencesAsync(fieldSymbol))
			{
				results.Add(CreateUsageMatch(item, tagName, newTagName, "TagField"));
			}
		}
		string[] array = new string[3]
		{
			"Has" + tagName + "Tag",
			"Add" + tagName + "Tag",
			"Del" + tagName + "Tag"
		};
		string[] array2 = array;
		foreach (string methodName in array2)
		{
			IMethodSymbol methodSymbol = apiSymbol.GetMembers(methodName).OfType<IMethodSymbol>().FirstOrDefault();
			if (methodSymbol == null)
			{
				continue;
			}
			List<SemanticUsage> obj = await FindSymbolReferencesAsync(methodSymbol);
			string newText = methodName.Replace(tagName, newTagName);
			foreach (SemanticUsage item2 in obj)
			{
				results.Add(CreateUsageMatch(item2, methodName, newText, "TagMethod"));
			}
		}
		await FindSourceDefinitionAsync(context, results);
		return results;
	}

	private async Task<List<UsageMatch>> FindValueUsagesAsync(RenameContext context)
	{
		List<UsageMatch> results = new List<UsageMatch>();
		string valueName = context.OldName;
		string newValueName = context.NewName;
		INamedTypeSymbol apiSymbol = await FindApiClassAsync(context.OwnerName, context.OwnerNamespace);
		if (apiSymbol == null)
		{
			Logger.LogWarning("Could not find API class '" + context.OwnerName + "' for semantic analysis");
			return results;
		}
		IFieldSymbol fieldSymbol = apiSymbol.GetMembers(valueName).OfType<IFieldSymbol>().FirstOrDefault((IFieldSymbol f) => f.IsStatic);
		if (fieldSymbol != null)
		{
			foreach (SemanticUsage item in await FindSymbolReferencesAsync(fieldSymbol))
			{
				results.Add(CreateUsageMatch(item, valueName, newValueName, "ValueField"));
			}
		}
		string[] array = new string[7] { "Get", "Set", "Has", "Del", "Add", "TryGet", "Ref" };
		string[] array2 = array;
		foreach (string prefix in array2)
		{
			string methodName = prefix + valueName;
			IMethodSymbol methodSymbol = apiSymbol.GetMembers(methodName).OfType<IMethodSymbol>().FirstOrDefault();
			if (methodSymbol == null)
			{
				continue;
			}
			List<SemanticUsage> obj = await FindSymbolReferencesAsync(methodSymbol);
			string newText = prefix + newValueName;
			foreach (SemanticUsage item2 in obj)
			{
				results.Add(CreateUsageMatch(item2, methodName, newText, prefix + "Method"));
			}
		}
		await FindSourceDefinitionAsync(context, results);
		return results;
	}

	private async Task<List<UsageMatch>> FindBehaviourUsagesAsync(RenameContext context)
	{
		List<UsageMatch> results = new List<UsageMatch>();
		string behaviourName = context.OldName;
		string newBehaviourName = context.NewName;
		INamedTypeSymbol namedTypeSymbol = null;
		if (!string.IsNullOrEmpty(context.SourceFilePath) && File.Exists(context.SourceFilePath))
		{
			namedTypeSymbol = await FindTypeInFileAsync(behaviourName, context.SourceFilePath);
		}
		if (namedTypeSymbol == null)
		{
			namedTypeSymbol = await FindTypeInSolutionAsync(behaviourName, null);
		}
		if (namedTypeSymbol == null)
		{
			Logger.LogWarning("Could not find behaviour class '" + behaviourName + "' for semantic analysis");
			return results;
		}
		ImmutableArray<Location>.Enumerator enumerator = namedTypeSymbol.Locations.GetEnumerator();
		while (enumerator.MoveNext())
		{
			Location current = enumerator.Current;
			if (current.IsInSource)
			{
				TextSpan sourceSpan = current.SourceSpan;
				SyntaxTree sourceTree = current.SourceTree;
				if (sourceTree != null)
				{
					SourceText text = sourceTree.GetText();
					LinePositionSpan linePositionSpan = text.Lines.GetLinePositionSpan(sourceSpan);
					string lineContext = text.Lines[linePositionSpan.Start.Line].ToString();
					results.Add(new UsageMatch
					{
						FilePath = sourceTree.FilePath,
						Line = linePositionSpan.Start.Line + 1,
						Column = linePositionSpan.Start.Character + 1,
						Length = behaviourName.Length,
						MatchedText = behaviourName,
						ReplacementText = newBehaviourName,
						LineContext = lineContext,
						Category = "ClassDeclaration",
						IsAmbiguous = false
					});
				}
			}
		}
		ImmutableArray<IMethodSymbol>.Enumerator enumerator2 = namedTypeSymbol.Constructors.GetEnumerator();
		while (enumerator2.MoveNext())
		{
			IMethodSymbol current2 = enumerator2.Current;
			if (current2.IsImplicitlyDeclared)
			{
				continue;
			}
			enumerator = current2.Locations.GetEnumerator();
			while (enumerator.MoveNext())
			{
				Location current3 = enumerator.Current;
				if (current3.IsInSource)
				{
					TextSpan sourceSpan2 = current3.SourceSpan;
					SyntaxTree sourceTree2 = current3.SourceTree;
					if (sourceTree2 != null)
					{
						SourceText text2 = sourceTree2.GetText();
						LinePositionSpan linePositionSpan2 = text2.Lines.GetLinePositionSpan(sourceSpan2);
						string lineContext2 = text2.Lines[linePositionSpan2.Start.Line].ToString();
						results.Add(new UsageMatch
						{
							FilePath = sourceTree2.FilePath,
							Line = linePositionSpan2.Start.Line + 1,
							Column = linePositionSpan2.Start.Character + 1,
							Length = behaviourName.Length,
							MatchedText = behaviourName,
							ReplacementText = newBehaviourName,
							LineContext = lineContext2,
							Category = "Constructor",
							IsAmbiguous = false
						});
					}
				}
			}
		}
		foreach (SemanticUsage item in await FindSymbolReferencesAsync(namedTypeSymbol))
		{
			string behaviourUsageCategory = GetBehaviourUsageCategory(item);
			results.Add(CreateUsageMatch(item, behaviourName, newBehaviourName, behaviourUsageCategory));
		}
		INamedTypeSymbol apiSymbol = await FindApiClassAsync(context.OwnerName, context.OwnerNamespace);
		if (apiSymbol != null)
		{
			string text3 = (behaviourName.EndsWith("Behaviour", StringComparison.OrdinalIgnoreCase) ? behaviourName.Substring(0, behaviourName.Length - "Behaviour".Length) : behaviourName);
			string text4 = (newBehaviourName.EndsWith("Behaviour", StringComparison.OrdinalIgnoreCase) ? newBehaviourName.Substring(0, newBehaviourName.Length - "Behaviour".Length) : newBehaviourName);
			string methodSuffix = text3 + "Behaviour";
			string newMethodSuffix = text4 + "Behaviour";
			string[] array = new string[5] { "Has", "Get", "Add", "Del", "TryGet" };
			string[] array2 = array;
			foreach (string prefix in array2)
			{
				string methodName = prefix + methodSuffix;
				IMethodSymbol methodSymbol = apiSymbol.GetMembers(methodName).OfType<IMethodSymbol>().FirstOrDefault();
				if (methodSymbol == null)
				{
					continue;
				}
				List<SemanticUsage> obj = await FindSymbolReferencesAsync(methodSymbol);
				string newText = prefix + newMethodSuffix;
				foreach (SemanticUsage item2 in obj)
				{
					results.Add(CreateUsageMatch(item2, methodName, newText, prefix + "Method"));
				}
			}
		}
		return results;
	}

	private string GetBehaviourUsageCategory(SemanticUsage usage)
	{
		if (usage.Context.Contains("new "))
		{
			return "Constructor";
		}
		if (usage.Context.Contains("typeof"))
		{
			return "TypeOf";
		}
		if (usage.Context.Contains("class "))
		{
			return "ClassDeclaration";
		}
		if (usage.Context.Contains("<") && usage.Context.Contains(">"))
		{
			return "GenericArg";
		}
		return "TypeReference";
	}

	private async Task<List<UsageMatch>> FindDomainUsagesAsync(RenameContext context)
	{
		List<UsageMatch> results = new List<UsageMatch>();
		string entityName = context.OldName;
		string newEntityName = context.NewName;
		string domainSourceFile = context.SourceFilePath;
		HashSet<string> generatedFiles = await FindGeneratedFilesAsync(domainSourceFile);
		generatedFiles.Add(Path.GetFullPath(domainSourceFile));
		Logger.LogVerbose($"Found {generatedFiles.Count} files generated by this domain");
		List<INamedTypeSymbol> list = await _workspace.GetDefinedTypesAsync(generatedFiles);
		Logger.LogVerbose($"Found {list.Count} defined types");
		foreach (INamedTypeSymbol item2 in list)
		{
			string typeName = item2.Name;
			if (!typeName.Contains(entityName))
			{
				continue;
			}
			string newTypeName = typeName.Replace(entityName, newEntityName);
			foreach (SemanticUsage item3 in await FindSymbolReferencesAsync(item2))
			{
				results.Add(CreateUsageMatch(item3, typeName, newTypeName, GetDomainTypeCategory(typeName, entityName)));
			}
		}
		await FindEntityNamePropertyAsync(domainSourceFile, entityName, newEntityName, results);
		foreach (string item4 in generatedFiles)
		{
			if (!item4.Equals(Path.GetFullPath(domainSourceFile), StringComparison.OrdinalIgnoreCase))
			{
				string fileName = Path.GetFileName(item4);
				if (fileName.Contains(entityName))
				{
					string path = fileName.Replace(entityName, newEntityName);
					string item = Path.Combine(Path.GetDirectoryName(item4) ?? "", path);
					context.FileRenames.Add((item4, item));
				}
			}
		}
		return results;
	}

	private async Task<HashSet<string>> FindGeneratedFilesAsync(string domainSourceFile)
	{
		HashSet<string> generatedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (Project project in _workspace.Projects)
		{
			foreach (Document doc in project.Documents)
			{
				if (doc.FilePath != null)
				{
					string content = await File.ReadAllTextAsync(doc.FilePath);
					if (IsGeneratedByDomain(doc.FilePath, content, domainSourceFile))
					{
						generatedFiles.Add(Path.GetFullPath(doc.FilePath));
					}
				}
			}
		}
		return generatedFiles;
	}

	private bool IsGeneratedByDomain(string filePath, string content, string domainSourceFile)
	{
		string text = Path.GetFullPath(filePath).Replace('/', '\\');
		string text2 = Path.GetFullPath(domainSourceFile).Replace('/', '\\');
		if (text.Equals(text2, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		string input = string.Join("\n", content.Split('\n').Take(10));
		Match match = SourceFilePathRegex.Match(input);
		if (!match.Success)
		{
			return false;
		}
		string text3 = match.Groups[1].Value.Trim().Replace('/', '\\');
		if (!Path.IsPathRooted(text3))
		{
			string fileName = Path.GetFileName(text2);
			if (text3.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		else
		{
			text3 = Path.GetFullPath(text3).Replace('/', '\\');
			if (text3.Equals(text2, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		return false;
	}

	private string GetDomainTypeCategory(string typeName, string entityName)
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

	private async Task FindEntityNamePropertyAsync(string filePath, string entityName, string newEntityName, List<UsageMatch> results)
	{
		if (!File.Exists(filePath))
		{
			return;
		}
		string[] array = (await File.ReadAllTextAsync(filePath)).Split('\n');
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

	private async Task<INamedTypeSymbol?> FindApiClassAsync(string className, string? namespaceName)
	{
		foreach (Project project in _workspace.Projects)
		{
			Compilation compilation = await project.GetCompilationAsync();
			if (compilation == null)
			{
				continue;
			}
			foreach (SyntaxTree syntaxTree in compilation.SyntaxTrees)
			{
				SemanticModel semanticModel = compilation.GetSemanticModel(syntaxTree);
				foreach (ClassDeclarationSyntax item in (await syntaxTree.GetRootAsync()).DescendantNodes().OfType<ClassDeclarationSyntax>())
				{
					if (item.Identifier.Text == className)
					{
						INamedTypeSymbol declaredSymbol = semanticModel.GetDeclaredSymbol(item);
						if (declaredSymbol != null && (string.IsNullOrEmpty(namespaceName) || declaredSymbol.ContainingNamespace?.ToDisplayString() == namespaceName))
						{
							return declaredSymbol;
						}
					}
				}
			}
		}
		return null;
	}

	private async Task<INamedTypeSymbol?> FindTypeInSolutionAsync(string typeName, string? namespaceName)
	{
		foreach (Project project in _workspace.Projects)
		{
			Compilation compilation = await project.GetCompilationAsync();
			if (compilation == null)
			{
				continue;
			}
			foreach (SyntaxTree syntaxTree in compilation.SyntaxTrees)
			{
				SemanticModel semanticModel = compilation.GetSemanticModel(syntaxTree);
				foreach (TypeDeclarationSyntax item in (await syntaxTree.GetRootAsync()).DescendantNodes().OfType<TypeDeclarationSyntax>())
				{
					if (item.Identifier.Text == typeName)
					{
						INamedTypeSymbol declaredSymbol = semanticModel.GetDeclaredSymbol(item);
						if (declaredSymbol != null && (string.IsNullOrEmpty(namespaceName) || declaredSymbol.ContainingNamespace?.ToDisplayString() == namespaceName))
						{
							return declaredSymbol;
						}
					}
				}
			}
		}
		return null;
	}

	private async Task<INamedTypeSymbol?> FindTypeInFileAsync(string typeName, string filePath)
	{
		string normalizedPath = Path.GetFullPath(filePath);
		foreach (Project project in _workspace.Projects)
		{
			Compilation compilation = await project.GetCompilationAsync();
			if (compilation == null)
			{
				continue;
			}
			foreach (Document document in project.Documents)
			{
				if (document.FilePath == null || !Path.GetFullPath(document.FilePath).Equals(normalizedPath, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				SyntaxTree syntaxTree = await document.GetSyntaxTreeAsync();
				if (syntaxTree == null)
				{
					continue;
				}
				SemanticModel semanticModel = compilation.GetSemanticModel(syntaxTree);
				foreach (TypeDeclarationSyntax item in (await syntaxTree.GetRootAsync()).DescendantNodes().OfType<TypeDeclarationSyntax>())
				{
					if (item.Identifier.Text == typeName)
					{
						INamedTypeSymbol declaredSymbol = semanticModel.GetDeclaredSymbol(item);
						if (declaredSymbol != null)
						{
							return declaredSymbol;
						}
					}
				}
			}
		}
		return null;
	}

	private async Task<List<SemanticUsage>> FindSymbolReferencesAsync(ISymbol symbol)
	{
		List<SemanticUsage> usages = new List<SemanticUsage>();
		if (_workspace?.Solution == null)
		{
			return usages;
		}
		try
		{
			foreach (ReferencedSymbol item in await SymbolFinder.FindReferencesAsync(symbol, _workspace.Solution))
			{
				foreach (ReferenceLocation location in item.Locations)
				{
					string filePath = location.Document.FilePath;
					if (filePath != null)
					{
						SourceText sourceText = await location.Document.GetTextAsync();
						TextSpan sourceSpan = location.Location.SourceSpan;
						LinePositionSpan linePositionSpan = sourceText.Lines.GetLinePositionSpan(sourceSpan);
						string context = sourceText.Lines[linePositionSpan.Start.Line].ToString();
						usages.Add(new SemanticUsage
						{
							FilePath = filePath,
							Line = linePositionSpan.Start.Line + 1,
							Column = linePositionSpan.Start.Character + 1,
							Length = sourceSpan.Length,
							SymbolName = sourceText.GetSubText(sourceSpan).ToString(),
							Context = context
						});
					}
				}
			}
		}
		catch (Exception ex)
		{
			Logger.LogVerbose("Error finding references for " + symbol.Name + ": " + ex.Message);
		}
		return usages;
	}

	private async Task FindSourceDefinitionAsync(RenameContext context, List<UsageMatch> results)
	{
		if (string.IsNullOrEmpty(context.SourceFilePath) || !File.Exists(context.SourceFilePath))
		{
			return;
		}
		string content = await File.ReadAllTextAsync(context.SourceFilePath);
		CSharpParseOptions options = CreateParseOptionsWithSymbols(content);
		SyntaxNode syntaxNode = await CSharpSyntaxTree.ParseText(content, options).GetRootAsync();
		if (context.Type == RenameType.Tag)
		{
			foreach (EnumDeclarationSyntax item in from e in syntaxNode.DescendantNodes().OfType<EnumDeclarationSyntax>()
				where e.Identifier.Text == "Tags"
				select e)
			{
				EnumMemberDeclarationSyntax enumMemberDeclarationSyntax = item.Members.FirstOrDefault((EnumMemberDeclarationSyntax m) => m.Identifier.Text == context.OldName);
				if (enumMemberDeclarationSyntax != null)
				{
					FileLinePositionSpan lineSpan = enumMemberDeclarationSyntax.Identifier.GetLocation().GetLineSpan();
					string text = content.Split('\n')[lineSpan.StartLinePosition.Line];
					results.Add(new UsageMatch
					{
						FilePath = context.SourceFilePath,
						Line = lineSpan.StartLinePosition.Line + 1,
						Column = lineSpan.StartLinePosition.Character + 1,
						Length = context.OldName.Length,
						MatchedText = context.OldName,
						ReplacementText = context.NewName,
						LineContext = text.TrimEnd('\r'),
						Category = "SourceDefinition",
						IsAmbiguous = false
					});
				}
			}
			return;
		}
		if (context.Type != RenameType.Value)
		{
			return;
		}
		foreach (ClassDeclarationSyntax item2 in from c in syntaxNode.DescendantNodes().OfType<ClassDeclarationSyntax>()
			where c.Identifier.Text == "Values"
			select c)
		{
			foreach (VariableDeclaratorSyntax item3 in from v in item2.DescendantNodes().OfType<VariableDeclaratorSyntax>()
				where v.Identifier.Text == context.OldName
				select v)
			{
				FileLinePositionSpan lineSpan2 = item3.Identifier.GetLocation().GetLineSpan();
				string text2 = content.Split('\n')[lineSpan2.StartLinePosition.Line];
				results.Add(new UsageMatch
				{
					FilePath = context.SourceFilePath,
					Line = lineSpan2.StartLinePosition.Line + 1,
					Column = lineSpan2.StartLinePosition.Character + 1,
					Length = context.OldName.Length,
					MatchedText = context.OldName,
					ReplacementText = context.NewName,
					LineContext = text2.TrimEnd('\r'),
					Category = "SourceDefinition",
					IsAmbiguous = false
				});
			}
		}
	}

	private static CSharpParseOptions CreateParseOptionsWithSymbols(string sourceCode)
	{
		HashSet<string> hashSet = new HashSet<string>();
		foreach (Match item in PreprocessorSymbolRegex.Matches(sourceCode))
		{
			string value = item.Groups[2].Value;
			if (!string.IsNullOrEmpty(value))
			{
				hashSet.Add(value);
			}
		}
		return new CSharpParseOptions(LanguageVersion.Default, DocumentationMode.Parse, SourceCodeKind.Regular, hashSet);
	}

	private UsageMatch CreateUsageMatch(SemanticUsage usage, string oldText, string newText, string category)
	{
		return new UsageMatch
		{
			FilePath = usage.FilePath,
			Line = usage.Line,
			Column = usage.Column,
			Length = usage.Length,
			MatchedText = usage.SymbolName,
			ReplacementText = newText,
			LineContext = usage.Context,
			Category = category,
			IsAmbiguous = false
		};
	}

	public static string? FindSolutionFile(string projectRoot)
	{
		return Directory.GetFiles(projectRoot, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();
	}
}
