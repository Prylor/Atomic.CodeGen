using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Atomic.CodeGen.Core.Models;
using Atomic.CodeGen.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Atomic.CodeGen.Roslyn;

public sealed class BehaviourParser
{
	private static readonly Regex PreprocessorSymbolRegex = new Regex("#(?:if|elif)\\s+(!?\\s*)?(\\w+)", RegexOptions.Multiline | RegexOptions.Compiled);

	private static readonly HashSet<string> ValidBehaviourBases = new HashSet<string>(StringComparer.Ordinal) { "IEntityBehaviour", "IEntityInit", "IEntityDispose", "IEntityEnable", "IEntityDisable", "IEntityTick", "IEntityFixedTick", "IEntityLateTick", "IEntityGizmos" };

	private static readonly string[] GenericBehaviourPrefixes = new string[8] { "IEntityInit<", "IEntityDispose<", "IEntityEnable<", "IEntityDisable<", "IEntityTick<", "IEntityFixedTick<", "IEntityLateTick<", "IEntityGizmos<" };

	public async Task<List<BehaviourDefinition>> ParseFileAsync(string filePath)
	{
		List<BehaviourDefinition> results = new List<BehaviourDefinition>();
		try
		{
			if (!File.Exists(filePath))
			{
				return results;
			}
			return ParseSource(filePath, await File.ReadAllTextAsync(filePath));
		}
		catch (Exception ex)
		{
			Logger.LogError("Failed to parse behaviours in " + filePath + ": " + ex.Message);
			return results;
		}
	}

	public List<BehaviourDefinition> ParseSource(string filePath, string sourceCode)
	{
		List<BehaviourDefinition> behaviours = new List<BehaviourDefinition>();
		CSharpParseOptions options = CreateParseOptionsWithSymbols(sourceCode);
		SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(sourceCode, options);
		CompilationUnitSyntax root = syntaxTree.GetRoot() as CompilationUnitSyntax;
		if (root == null)
		{
			return behaviours;
		}
		bool hasAtomicEntitiesUsing = root.Usings.Any((UsingDirectiveSyntax u) => u.Name?.ToString() == "Atomic.Entities");
		foreach (ClassDeclarationSyntax classDecl in from c in root.DescendantNodes().OfType<ClassDeclarationSyntax>()
			where HasLinkToAttribute(c, root)
			select c)
		{
			BehaviourDefinition behaviourDefinition = ParseBehaviourClass(filePath, root, classDecl, hasAtomicEntitiesUsing);
			if (behaviourDefinition != null)
			{
				behaviours.Add(behaviourDefinition);
			}
		}
		return behaviours;
	}

	private BehaviourDefinition? ParseBehaviourClass(string filePath, CompilationUnitSyntax root, ClassDeclarationSyntax classDecl, bool hasAtomicEntitiesUsing)
	{
		AttributeSyntax linkToAttribute = GetLinkToAttribute(classDecl, hasAtomicEntitiesUsing);
		if (linkToAttribute == null)
		{
			return null;
		}
		string linkedApiTypeName = ExtractLinkedApiTypeName(linkToAttribute);
		if (string.IsNullOrEmpty(linkedApiTypeName))
		{
			Logger.LogWarning("[LinkTo] attribute in " + filePath + " has no valid type argument");
			return null;
		}
		string className = classDecl.Identifier.Text;
		string namespaceName = GetNamespace(classDecl);
		List<(string, string)> constructorParameters = ExtractConstructorParameters(classDecl);
		List<string> requiredImports = ImportExtractor.Extract(root, Array.Empty<string>());
		BehaviourDefinition behaviourDefinition = new BehaviourDefinition
		{
			SourceFile = filePath,
			LinkedApiTypeName = linkedApiTypeName,
			Namespace = namespaceName,
			ClassName = className,
			ConstructorParameters = constructorParameters,
			RequiredImports = requiredImports
		};
		ValidateBehaviourClass(classDecl, behaviourDefinition);
		if (!behaviourDefinition.IsValid)
		{
			foreach (string error in behaviourDefinition.Errors)
			{
				Logger.LogError($"Behaviour {className} in {filePath}: {error}");
			}
			return null;
		}
		Logger.LogVerbose($"Parsed Behaviour: {className} -> {linkedApiTypeName} from {filePath}");
		return behaviourDefinition;
	}

	private void ValidateBehaviourClass(ClassDeclarationSyntax classDecl, BehaviourDefinition definition)
	{
		if (classDecl.BaseList == null || !classDecl.BaseList.Types.Any())
		{
			definition.Errors.Add("Class must implement IEntityBehaviour or a derived interface (IEntityInit, IEntityTick, etc.)");
			return;
		}
		List<string> baseTypeNames = classDecl.BaseList.Types.Select((BaseTypeSyntax t) => t.Type.ToString()).ToList();
		if (!baseTypeNames.Any(IsValidBehaviourBase))
		{
			Logger.LogVerbose($"Behaviour {definition.ClassName} uses non-standard base types: {string.Join(", ", baseTypeNames)}. " + "Assuming domain-specific interfaces that derive from IEntityBehaviour.");
		}
		if (classDecl.Modifiers.Any((SyntaxToken m) => m.IsKind(SyntaxKind.AbstractKeyword)))
		{
			definition.Errors.Add("Behaviour class cannot be abstract");
		}
		if (classDecl.Modifiers.Any((SyntaxToken m) => m.IsKind(SyntaxKind.StaticKeyword)))
		{
			definition.Errors.Add("Behaviour class cannot be static");
		}
	}

	private static bool IsValidBehaviourBase(string baseType)
	{
		if (ValidBehaviourBases.Contains(baseType))
		{
			return true;
		}
		string[] genericBehaviourPrefixes = GenericBehaviourPrefixes;
		foreach (string prefix in genericBehaviourPrefixes)
		{
			if (baseType.StartsWith(prefix, StringComparison.Ordinal))
			{
				return true;
			}
		}
		return false;
	}

	private List<(string Name, string Type)> ExtractConstructorParameters(ClassDeclarationSyntax classDecl)
	{
		List<(string, string)> parameters = new List<(string, string)>();
		List<ConstructorDeclarationSyntax> allConstructors = classDecl.Members.OfType<ConstructorDeclarationSyntax>().ToList();
		if (allConstructors.Count == 0)
		{
			return parameters;
		}
		List<ConstructorDeclarationSyntax> publicConstructors = allConstructors.Where((ConstructorDeclarationSyntax c) => c.Modifiers.Any((SyntaxToken m) => m.IsKind(SyntaxKind.PublicKeyword))).ToList();
		ConstructorDeclarationSyntax selectedConstructor = null;
		if (publicConstructors.Count > 0)
		{
			selectedConstructor = publicConstructors.OrderByDescending((ConstructorDeclarationSyntax c) => c.ParameterList.Parameters.Count).First();
		}
		else if (allConstructors.Count == 1)
		{
			selectedConstructor = allConstructors[0];
		}
		if (selectedConstructor == null || selectedConstructor.ParameterList.Parameters.Count == 0)
		{
			return parameters;
		}
		foreach (ParameterSyntax param in selectedConstructor.ParameterList.Parameters)
		{
			string paramName = param.Identifier.Text;
			string paramType = param.Type?.ToString() ?? "object";
			parameters.Add((paramName, paramType));
		}
		return parameters;
	}

	private string ExtractLinkedApiTypeName(AttributeSyntax attribute)
	{
		SeparatedSyntaxList<AttributeArgumentSyntax>? separatedSyntaxList = attribute.ArgumentList?.Arguments;
		if (!separatedSyntaxList.HasValue || separatedSyntaxList.Value.Count == 0)
		{
			return string.Empty;
		}
		if (separatedSyntaxList.Value[0].Expression is TypeOfExpressionSyntax typeOfExpressionSyntax)
		{
			return typeOfExpressionSyntax.Type.ToString();
		}
		return string.Empty;
	}

	private static bool HasLinkToAttribute(ClassDeclarationSyntax classDecl, CompilationUnitSyntax root)
	{
		bool hasAtomicEntitiesUsing = root.Usings.Any((UsingDirectiveSyntax u) => u.Name?.ToString() == "Atomic.Entities");
		return classDecl.AttributeLists.SelectMany((AttributeListSyntax al) => al.Attributes).Any((AttributeSyntax a) => IsAtomicLinkToAttribute(a, hasAtomicEntitiesUsing));
	}

	private static bool IsAtomicLinkToAttribute(AttributeSyntax attr, bool hasAtomicEntitiesUsing)
	{
		string attributeName = attr.Name.ToString();
		if (attributeName == "Atomic.Entities.LinkTo" || attributeName == "Atomic.Entities.LinkToAttribute")
		{
			return true;
		}
		bool isLinkTo = hasAtomicEntitiesUsing;
		if (isLinkTo)
		{
			bool isShortName = attributeName == "LinkTo" || attributeName == "LinkToAttribute";
			isLinkTo = isShortName;
		}
		if (isLinkTo)
		{
			return true;
		}
		return false;
	}

	private static AttributeSyntax? GetLinkToAttribute(ClassDeclarationSyntax classDecl, bool hasAtomicEntitiesUsing)
	{
		return classDecl.AttributeLists.SelectMany((AttributeListSyntax al) => al.Attributes).FirstOrDefault((AttributeSyntax a) => IsAtomicLinkToAttribute(a, hasAtomicEntitiesUsing));
	}

	private static string GetNamespace(ClassDeclarationSyntax classDecl)
	{
		SyntaxNode parent = classDecl.Parent;
		while (parent != null)
		{
			if (!(parent is NamespaceDeclarationSyntax namespaceDeclarationSyntax))
			{
				if (parent is FileScopedNamespaceDeclarationSyntax fileScopedNamespaceDeclarationSyntax)
				{
					return fileScopedNamespaceDeclarationSyntax.Name.ToString();
				}
				parent = parent.Parent;
				continue;
			}
			return namespaceDeclarationSyntax.Name.ToString();
		}
		return string.Empty;
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
