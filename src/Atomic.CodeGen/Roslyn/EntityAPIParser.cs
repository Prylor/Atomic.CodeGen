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

public sealed class EntityAPIParser
{
	private static readonly Regex PreprocessorSymbolRegex = new Regex("#(?:if|elif)\\s+(!?\\s*)?(\\w+)", RegexOptions.Multiline | RegexOptions.Compiled);

	public async Task<EntityAPIDefinition?> ParseFileAsync(string filePath)
	{
		try
		{
			if (!File.Exists(filePath))
			{
				Logger.LogWarning("File not found: " + filePath);
				return null;
			}
			return ParseSource(filePath, await File.ReadAllTextAsync(filePath));
		}
		catch (Exception ex)
		{
			Logger.LogError("Failed to parse " + filePath + ": " + ex.Message);
			return null;
		}
	}

	public EntityAPIDefinition? ParseSource(string filePath, string sourceCode)
	{
		CSharpParseOptions options = CreateParseOptionsWithSymbols(sourceCode);
		SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(sourceCode, options);
		CompilationUnitSyntax root = syntaxTree.GetRoot() as CompilationUnitSyntax;
		if (root == null)
		{
			Logger.LogWarning("Failed to parse syntax tree: " + filePath);
			return null;
		}
		ClassDeclarationSyntax classDeclarationSyntax = root.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault((ClassDeclarationSyntax c) => HasEntityAPIAttribute(c, root));
		if (classDeclarationSyntax == null)
		{
			Logger.LogVerbose("No [EntityAPI] attribute found in: " + filePath);
			return null;
		}
		bool hasAtomicEntitiesUsing = root.Usings.Any((UsingDirectiveSyntax u) => u.Name?.ToString() == "Atomic.Entities");
		AttributeSyntax entityAPIAttribute = GetEntityAPIAttribute(classDeclarationSyntax, hasAtomicEntitiesUsing);
		if (entityAPIAttribute == null)
		{
			Logger.LogWarning("Failed to extract attribute from: " + filePath);
			return null;
		}
		Dictionary<string, object> args = AttributeParser.Parse(entityAPIAttribute);
		List<string> tags = TypeExtractor.ExtractTags(classDeclarationSyntax);
		Dictionary<string, string> values = TypeExtractor.ExtractValues(classDeclarationSyntax);
		string[] stringArray = AttributeParser.GetStringArray(args, "ExcludeImports");
		List<string> imports = ImportExtractor.Extract(root, stringArray);
		string text = GetNamespace(classDeclarationSyntax);
		string text2 = classDeclarationSyntax.Identifier.Text;
		string text3 = AttributeParser.GetString(args, "Namespace");
		string text4 = AttributeParser.GetString(args, "ClassName");
		string text5 = AttributeParser.GetString(args, "Directory");
		string text6 = Path.GetDirectoryName(filePath) ?? string.Empty;
		EntityAPIDefinition entityAPIDefinition = new EntityAPIDefinition
		{
			SourceFile = filePath,
			Namespace = ((!string.IsNullOrWhiteSpace(text3)) ? text3 : text),
			ClassName = ((!string.IsNullOrWhiteSpace(text4)) ? text4 : text2),
			Directory = ((!string.IsNullOrWhiteSpace(text5)) ? text5 : text6),
			TargetProject = AttributeParser.GetString(args, "TargetProject"),
			EntityType = AttributeParser.GetTypeName(args, "EntityType"),
			AggressiveInlining = AttributeParser.GetBool(args, "AggressiveInlining", defaultValue: true),
			UnsafeAccess = AttributeParser.GetBool(args, "UnsafeAccess", defaultValue: true),
			Imports = imports,
			Tags = tags,
			Values = values
		};
		entityAPIDefinition.Validate();
		if (!entityAPIDefinition.IsValid)
		{
			Logger.LogError("Invalid EntityAPI definition in " + filePath + ":");
			foreach (string error in entityAPIDefinition.Errors)
			{
				Logger.LogError("  - " + error);
			}
			return null;
		}
		Logger.LogVerbose("Parsed EntityAPI: " + entityAPIDefinition.ClassName + " from " + filePath);
		return entityAPIDefinition;
	}

	private static bool HasEntityAPIAttribute(ClassDeclarationSyntax classDecl, CompilationUnitSyntax root)
	{
		bool hasAtomicEntitiesUsing = root.Usings.Any((UsingDirectiveSyntax u) => u.Name?.ToString() == "Atomic.Entities");
		return classDecl.AttributeLists.SelectMany((AttributeListSyntax al) => al.Attributes).Any((AttributeSyntax a) => IsAtomicEntityAPIAttribute(a, hasAtomicEntitiesUsing));
	}

	private static bool IsAtomicEntityAPIAttribute(AttributeSyntax attr, bool hasAtomicEntitiesUsing)
	{
		string text = attr.Name.ToString();
		if ((text == "Atomic.Entities.EntityAPI" || text == "Atomic.Entities.EntityAPIAttribute") ? true : false)
		{
			return true;
		}
		bool flag = hasAtomicEntitiesUsing;
		if (flag)
		{
			bool flag2 = ((text == "EntityAPI" || text == "EntityAPIAttribute") ? true : false);
			flag = flag2;
		}
		if (flag)
		{
			return true;
		}
		return false;
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

	private static AttributeSyntax? GetEntityAPIAttribute(ClassDeclarationSyntax classDecl, bool hasAtomicEntitiesUsing)
	{
		return classDecl.AttributeLists.SelectMany((AttributeListSyntax al) => al.Attributes).FirstOrDefault((AttributeSyntax a) => IsAtomicEntityAPIAttribute(a, hasAtomicEntitiesUsing));
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
