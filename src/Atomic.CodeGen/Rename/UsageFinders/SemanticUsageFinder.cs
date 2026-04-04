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
			Logger.LogWarning($"Could not find API class '{context.OwnerName}' for semantic analysis");
			return results;
		}
		IFieldSymbol fieldSymbol = apiSymbol.GetMembers(tagName).OfType<IFieldSymbol>().FirstOrDefault((IFieldSymbol f) => f.IsStatic);
		if (fieldSymbol != null)
		{
			foreach (SemanticUsage fieldUsage in await FindSymbolReferencesAsync(fieldSymbol))
			{
				results.Add(CreateUsageMatch(fieldUsage, tagName, newTagName, "TagField"));
			}
		}
		string[] tagMethods =
		[
			"Has" + tagName + "Tag",
			"Add" + tagName + "Tag",
			"Del" + tagName + "Tag"
		];
		foreach (string methodName in tagMethods)
		{
			IMethodSymbol methodSymbol = apiSymbol.GetMembers(methodName).OfType<IMethodSymbol>().FirstOrDefault();
			if (methodSymbol == null)
			{
				continue;
			}
			List<SemanticUsage> methodUsages = await FindSymbolReferencesAsync(methodSymbol);
			string newMethodName = methodName.Replace(tagName, newTagName);
			foreach (SemanticUsage methodUsage in methodUsages)
			{
				results.Add(CreateUsageMatch(methodUsage, methodName, newMethodName, "TagMethod"));
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
			Logger.LogWarning($"Could not find API class '{context.OwnerName}' for semantic analysis");
			return results;
		}
		IFieldSymbol fieldSymbol = apiSymbol.GetMembers(valueName).OfType<IFieldSymbol>().FirstOrDefault((IFieldSymbol f) => f.IsStatic);
		if (fieldSymbol != null)
		{
			foreach (SemanticUsage fieldUsage in await FindSymbolReferencesAsync(fieldSymbol))
			{
				results.Add(CreateUsageMatch(fieldUsage, valueName, newValueName, "ValueField"));
			}
		}
		string[] valuePrefixes = ["Get", "Set", "Has", "Del", "Add", "TryGet", "Ref"];
		foreach (string prefix in valuePrefixes)
		{
			string methodName = prefix + valueName;
			IMethodSymbol methodSymbol = apiSymbol.GetMembers(methodName).OfType<IMethodSymbol>().FirstOrDefault();
			if (methodSymbol == null)
			{
				continue;
			}
			List<SemanticUsage> methodUsages = await FindSymbolReferencesAsync(methodSymbol);
			string newMethodName = prefix + newValueName;
			foreach (SemanticUsage methodUsage in methodUsages)
			{
				results.Add(CreateUsageMatch(methodUsage, methodName, newMethodName, prefix + "Method"));
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
			Logger.LogWarning($"Could not find behaviour class '{behaviourName}' for semantic analysis");
			return results;
		}
		foreach (Location location in namedTypeSymbol.Locations)
		{
			if (location.IsInSource)
			{
				TextSpan sourceSpan = location.SourceSpan;
				SyntaxTree sourceTree = location.SourceTree;
				if (sourceTree != null)
				{
					SourceText sourceText = sourceTree.GetText();
					LinePositionSpan linePositionSpan = sourceText.Lines.GetLinePositionSpan(sourceSpan);
					string lineContext = sourceText.Lines[linePositionSpan.Start.Line].ToString();
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
		foreach (IMethodSymbol constructor in namedTypeSymbol.Constructors)
		{
			if (constructor.IsImplicitlyDeclared)
			{
				continue;
			}
			foreach (Location ctorLocation in constructor.Locations)
			{
				if (ctorLocation.IsInSource)
				{
					TextSpan sourceSpan2 = ctorLocation.SourceSpan;
					SyntaxTree sourceTree2 = ctorLocation.SourceTree;
					if (sourceTree2 != null)
					{
						SourceText ctorSourceText = sourceTree2.GetText();
						LinePositionSpan linePositionSpan2 = ctorSourceText.Lines.GetLinePositionSpan(sourceSpan2);
						string lineContext2 = ctorSourceText.Lines[linePositionSpan2.Start.Line].ToString();
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
		foreach (SemanticUsage typeUsage in await FindSymbolReferencesAsync(namedTypeSymbol))
		{
			string behaviourUsageCategory = GetBehaviourUsageCategory(typeUsage);
			results.Add(CreateUsageMatch(typeUsage, behaviourName, newBehaviourName, behaviourUsageCategory));
		}
		INamedTypeSymbol apiSymbol = await FindApiClassAsync(context.OwnerName, context.OwnerNamespace);
		if (apiSymbol != null)
		{
			string oldBaseName = (behaviourName.EndsWith("Behaviour", StringComparison.OrdinalIgnoreCase) ? behaviourName.Substring(0, behaviourName.Length - "Behaviour".Length) : behaviourName);
			string newBaseName = (newBehaviourName.EndsWith("Behaviour", StringComparison.OrdinalIgnoreCase) ? newBehaviourName.Substring(0, newBehaviourName.Length - "Behaviour".Length) : newBehaviourName);
			string methodSuffix = oldBaseName + "Behaviour";
			string newMethodSuffix = newBaseName + "Behaviour";
			string[] behaviourPrefixes = ["Has", "Get", "Add", "Del", "TryGet"];
			foreach (string prefix in behaviourPrefixes)
			{
				string methodName = prefix + methodSuffix;
				IMethodSymbol methodSymbol = apiSymbol.GetMembers(methodName).OfType<IMethodSymbol>().FirstOrDefault();
				if (methodSymbol == null)
				{
					continue;
				}
				List<SemanticUsage> methodUsages = await FindSymbolReferencesAsync(methodSymbol);
				string newMethodName = prefix + newMethodSuffix;
				foreach (SemanticUsage methodUsage in methodUsages)
				{
					results.Add(CreateUsageMatch(methodUsage, methodName, newMethodName, prefix + "Method"));
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
		List<INamedTypeSymbol> definedTypes = await _workspace.GetDefinedTypesAsync(generatedFiles);
		Logger.LogVerbose($"Found {definedTypes.Count} defined types");
		foreach (INamedTypeSymbol typeSymbol in definedTypes)
		{
			string typeName = typeSymbol.Name;
			if (!typeName.Contains(entityName))
			{
				continue;
			}
			string newTypeName = typeName.Replace(entityName, newEntityName);
			foreach (SemanticUsage typeUsage in await FindSymbolReferencesAsync(typeSymbol))
			{
				results.Add(CreateUsageMatch(typeUsage, typeName, newTypeName, GetDomainTypeCategory(typeName, entityName)));
			}
		}
		await FindEntityNamePropertyAsync(domainSourceFile, entityName, newEntityName, results);
		foreach (string generatedFile in generatedFiles)
		{
			if (!generatedFile.Equals(Path.GetFullPath(domainSourceFile), StringComparison.OrdinalIgnoreCase))
			{
				string fileName = Path.GetFileName(generatedFile);
				if (fileName.Contains(entityName))
				{
					string renamedFileName = fileName.Replace(entityName, newEntityName);
					string renamedFilePath = Path.Combine(Path.GetDirectoryName(generatedFile) ?? "", renamedFileName);
					context.FileRenames.Add((generatedFile, renamedFilePath));
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
		string normalizedFilePath = Path.GetFullPath(filePath).Replace('/', '\\');
		string normalizedDomainPath = Path.GetFullPath(domainSourceFile).Replace('/', '\\');
		if (normalizedFilePath.Equals(normalizedDomainPath, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		string input = string.Join("\n", content.Split('\n').Take(10));
		Match match = SourceFilePathRegex.Match(input);
		if (!match.Success)
		{
			return false;
		}
		string referencedSourcePath = match.Groups[1].Value.Trim().Replace('/', '\\');
		if (!Path.IsPathRooted(referencedSourcePath))
		{
			string fileName = Path.GetFileName(normalizedDomainPath);
			if (referencedSourcePath.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		else
		{
			referencedSourcePath = Path.GetFullPath(referencedSourcePath).Replace('/', '\\');
			if (referencedSourcePath.Equals(normalizedDomainPath, StringComparison.OrdinalIgnoreCase))
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
		string[] sourceLines = (await File.ReadAllTextAsync(filePath)).Split('\n');
		Regex regex = new Regex("EntityName\\s*=>\\s*\"" + Regex.Escape(entityName) + "\"");
		int lineNumber = 0;
		foreach (string line in sourceLines)
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
					ReplacementText = $"EntityName => \"{newEntityName}\"",
					LineContext = line.TrimEnd('\r'),
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
				foreach (ClassDeclarationSyntax classDecl in (await syntaxTree.GetRootAsync()).DescendantNodes().OfType<ClassDeclarationSyntax>())
				{
					if (classDecl.Identifier.Text == className)
					{
						INamedTypeSymbol declaredSymbol = semanticModel.GetDeclaredSymbol(classDecl);
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
				foreach (TypeDeclarationSyntax typeDecl in (await syntaxTree.GetRootAsync()).DescendantNodes().OfType<TypeDeclarationSyntax>())
				{
					if (typeDecl.Identifier.Text == typeName)
					{
						INamedTypeSymbol declaredSymbol = semanticModel.GetDeclaredSymbol(typeDecl);
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
				foreach (TypeDeclarationSyntax typeDecl in (await syntaxTree.GetRootAsync()).DescendantNodes().OfType<TypeDeclarationSyntax>())
				{
					if (typeDecl.Identifier.Text == typeName)
					{
						INamedTypeSymbol declaredSymbol = semanticModel.GetDeclaredSymbol(typeDecl);
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
			foreach (ReferencedSymbol referencedSymbol in await SymbolFinder.FindReferencesAsync(symbol, _workspace.Solution))
			{
				foreach (ReferenceLocation location in referencedSymbol.Locations)
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
			Logger.LogVerbose($"Error finding references for {symbol.Name}: {ex.Message}");
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
			foreach (EnumDeclarationSyntax tagsEnum in from e in syntaxNode.DescendantNodes().OfType<EnumDeclarationSyntax>()
				where e.Identifier.Text == "Tags"
				select e)
			{
				EnumMemberDeclarationSyntax enumMemberDeclarationSyntax = tagsEnum.Members.FirstOrDefault((EnumMemberDeclarationSyntax m) => m.Identifier.Text == context.OldName);
				if (enumMemberDeclarationSyntax != null)
				{
					FileLinePositionSpan lineSpan = enumMemberDeclarationSyntax.Identifier.GetLocation().GetLineSpan();
					string lineContext = content.Split('\n')[lineSpan.StartLinePosition.Line];
					results.Add(new UsageMatch
					{
						FilePath = context.SourceFilePath,
						Line = lineSpan.StartLinePosition.Line + 1,
						Column = lineSpan.StartLinePosition.Character + 1,
						Length = context.OldName.Length,
						MatchedText = context.OldName,
						ReplacementText = context.NewName,
						LineContext = lineContext.TrimEnd('\r'),
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
		foreach (ClassDeclarationSyntax valuesClass in from c in syntaxNode.DescendantNodes().OfType<ClassDeclarationSyntax>()
			where c.Identifier.Text == "Values"
			select c)
		{
			foreach (VariableDeclaratorSyntax valueDeclarator in from v in valuesClass.DescendantNodes().OfType<VariableDeclaratorSyntax>()
				where v.Identifier.Text == context.OldName
				select v)
			{
				FileLinePositionSpan lineSpan2 = valueDeclarator.Identifier.GetLocation().GetLineSpan();
				string lineContext = content.Split('\n')[lineSpan2.StartLinePosition.Line];
				results.Add(new UsageMatch
				{
					FilePath = context.SourceFilePath,
					Line = lineSpan2.StartLinePosition.Line + 1,
					Column = lineSpan2.StartLinePosition.Character + 1,
					Length = context.OldName.Length,
					MatchedText = context.OldName,
					ReplacementText = context.NewName,
					LineContext = lineContext.TrimEnd('\r'),
					Category = "SourceDefinition",
					IsAmbiguous = false
				});
			}
		}
	}

	private static CSharpParseOptions CreateParseOptionsWithSymbols(string sourceCode)
	{
		HashSet<string> preprocessorSymbols = new HashSet<string>();
		foreach (Match symbolMatch in PreprocessorSymbolRegex.Matches(sourceCode))
		{
			string symbolName = symbolMatch.Groups[2].Value;
			if (!string.IsNullOrEmpty(symbolName))
			{
				preprocessorSymbols.Add(symbolName);
			}
		}
		return new CSharpParseOptions(LanguageVersion.Default, DocumentationMode.Parse, SourceCodeKind.Regular, preprocessorSymbols);
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
		return Directory.GetFiles(projectRoot, "*.sln", SearchOption.TopDirectoryOnly)
			.Concat(Directory.GetFiles(projectRoot, "*.slnx", SearchOption.TopDirectoryOnly))
			.FirstOrDefault();
	}
}
