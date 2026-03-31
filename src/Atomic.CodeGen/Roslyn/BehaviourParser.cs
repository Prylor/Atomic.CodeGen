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
		List<BehaviourDefinition> list = new List<BehaviourDefinition>();
		CSharpParseOptions options = CreateParseOptionsWithSymbols(sourceCode);
		SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(sourceCode, options);
		CompilationUnitSyntax root = syntaxTree.GetRoot() as CompilationUnitSyntax;
		if (root == null)
		{
			return list;
		}
		bool hasAtomicEntitiesUsing = root.Usings.Any((UsingDirectiveSyntax u) => u.Name?.ToString() == "Atomic.Entities");
		foreach (ClassDeclarationSyntax item in from c in root.DescendantNodes().OfType<ClassDeclarationSyntax>()
			where HasLinkToAttribute(c, root)
			select c)
		{
			BehaviourDefinition behaviourDefinition = ParseBehaviourClass(filePath, root, item, hasAtomicEntitiesUsing);
			if (behaviourDefinition != null)
			{
				list.Add(behaviourDefinition);
			}
		}
		return list;
	}

	private BehaviourDefinition? ParseBehaviourClass(string filePath, CompilationUnitSyntax root, ClassDeclarationSyntax classDecl, bool hasAtomicEntitiesUsing)
	{
		AttributeSyntax linkToAttribute = GetLinkToAttribute(classDecl, hasAtomicEntitiesUsing);
		if (linkToAttribute == null)
		{
			return null;
		}
		string text = ExtractLinkedApiTypeName(linkToAttribute);
		if (string.IsNullOrEmpty(text))
		{
			Logger.LogWarning("[LinkTo] attribute in " + filePath + " has no valid type argument");
			return null;
		}
		string text2 = classDecl.Identifier.Text;
		string text3 = GetNamespace(classDecl);
		List<(string, string)> constructorParameters = ExtractConstructorParameters(classDecl);
		List<string> requiredImports = ImportExtractor.Extract(root, Array.Empty<string>());
		BehaviourDefinition behaviourDefinition = new BehaviourDefinition
		{
			SourceFile = filePath,
			LinkedApiTypeName = text,
			Namespace = text3,
			ClassName = text2,
			ConstructorParameters = constructorParameters,
			RequiredImports = requiredImports
		};
		ValidateBehaviourClass(classDecl, behaviourDefinition);
		if (!behaviourDefinition.IsValid)
		{
			foreach (string error in behaviourDefinition.Errors)
			{
				Logger.LogError($"Behaviour {text2} in {filePath}: {error}");
			}
			return null;
		}
		Logger.LogVerbose($"Parsed Behaviour: {text2} -> {text} from {filePath}");
		return behaviourDefinition;
	}

	private void ValidateBehaviourClass(ClassDeclarationSyntax classDecl, BehaviourDefinition definition)
	{
		if (classDecl.BaseList == null || !classDecl.BaseList.Types.Any())
		{
			definition.Errors.Add("Class must implement IEntityBehaviour or a derived interface (IEntityInit, IEntityTick, etc.)");
			return;
		}
		List<string> list = classDecl.BaseList.Types.Select((BaseTypeSyntax t) => t.Type.ToString()).ToList();
		if (!list.Any(IsValidBehaviourBase))
		{
			Logger.LogVerbose($"Behaviour {definition.ClassName} uses non-standard base types: {string.Join(", ", list)}. " + "Assuming domain-specific interfaces that derive from IEntityBehaviour.");
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
		foreach (string value in genericBehaviourPrefixes)
		{
			if (baseType.StartsWith(value, StringComparison.Ordinal))
			{
				return true;
			}
		}
		return false;
	}

	private List<(string Name, string Type)> ExtractConstructorParameters(ClassDeclarationSyntax classDecl)
	{
		List<(string, string)> list = new List<(string, string)>();
		List<ConstructorDeclarationSyntax> list2 = classDecl.Members.OfType<ConstructorDeclarationSyntax>().ToList();
		if (list2.Count == 0)
		{
			return list;
		}
		List<ConstructorDeclarationSyntax> list3 = list2.Where((ConstructorDeclarationSyntax c) => c.Modifiers.Any((SyntaxToken m) => m.IsKind(SyntaxKind.PublicKeyword))).ToList();
		ConstructorDeclarationSyntax constructorDeclarationSyntax = null;
		if (list3.Count > 0)
		{
			constructorDeclarationSyntax = list3.OrderByDescending((ConstructorDeclarationSyntax c) => c.ParameterList.Parameters.Count).First();
		}
		else if (list2.Count == 1)
		{
			constructorDeclarationSyntax = list2[0];
		}
		if (constructorDeclarationSyntax == null || constructorDeclarationSyntax.ParameterList.Parameters.Count == 0)
		{
			return list;
		}
		SeparatedSyntaxList<ParameterSyntax>.Enumerator enumerator = constructorDeclarationSyntax.ParameterList.Parameters.GetEnumerator();
		while (enumerator.MoveNext())
		{
			ParameterSyntax current = enumerator.Current;
			string text = current.Identifier.Text;
			string item = current.Type?.ToString() ?? "object";
			list.Add((text, item));
		}
		return list;
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
		string text = attr.Name.ToString();
		if (text == "Atomic.Entities.LinkTo" || text == "Atomic.Entities.LinkToAttribute")
		{
			return true;
		}
		bool flag = hasAtomicEntitiesUsing;
		if (flag)
		{
			bool flag2 = text == "LinkTo" || text == "LinkToAttribute";
			flag = flag2;
		}
		if (flag)
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
}
