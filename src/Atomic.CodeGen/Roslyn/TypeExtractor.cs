using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Atomic.CodeGen.Roslyn;

public static class TypeExtractor
{
	public static List<string> ExtractTags(ClassDeclarationSyntax classDecl)
	{
		EnumDeclarationSyntax enumDeclarationSyntax = classDecl.Members.OfType<EnumDeclarationSyntax>().FirstOrDefault((EnumDeclarationSyntax e) => e.Identifier.Text == "Tags");
		if (enumDeclarationSyntax == null)
		{
			return new List<string>();
		}
		return enumDeclarationSyntax.Members.Select((EnumMemberDeclarationSyntax m) => m.Identifier.Text).ToList();
	}

	public static Dictionary<string, string> ExtractValues(ClassDeclarationSyntax classDecl)
	{
		ClassDeclarationSyntax valuesClass = classDecl.Members.OfType<ClassDeclarationSyntax>().FirstOrDefault((ClassDeclarationSyntax c) => c.Identifier.Text == "Values");
		if (valuesClass == null)
		{
			return new Dictionary<string, string>();
		}
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		foreach (FieldDeclarationSyntax fieldDecl in valuesClass.Members.OfType<FieldDeclarationSyntax>())
		{
			string fieldTypeName = fieldDecl.Declaration.Type.ToString();
			foreach (VariableDeclaratorSyntax variable in fieldDecl.Declaration.Variables)
			{
				dictionary[variable.Identifier.Text] = fieldTypeName;
			}
		}
		foreach (PropertyDeclarationSyntax propertyDecl in valuesClass.Members.OfType<PropertyDeclarationSyntax>())
		{
			string propertyTypeName = propertyDecl.Type.ToString();
			dictionary[propertyDecl.Identifier.Text] = propertyTypeName;
		}
		return dictionary;
	}
}
