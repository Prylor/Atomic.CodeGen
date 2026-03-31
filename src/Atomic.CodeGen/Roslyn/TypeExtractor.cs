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
		ClassDeclarationSyntax classDeclarationSyntax = classDecl.Members.OfType<ClassDeclarationSyntax>().FirstOrDefault((ClassDeclarationSyntax c) => c.Identifier.Text == "Values");
		if (classDeclarationSyntax == null)
		{
			return new Dictionary<string, string>();
		}
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		foreach (FieldDeclarationSyntax item in classDeclarationSyntax.Members.OfType<FieldDeclarationSyntax>())
		{
			string value = item.Declaration.Type.ToString();
			SeparatedSyntaxList<VariableDeclaratorSyntax>.Enumerator enumerator2 = item.Declaration.Variables.GetEnumerator();
			while (enumerator2.MoveNext())
			{
				VariableDeclaratorSyntax current2 = enumerator2.Current;
				dictionary[current2.Identifier.Text] = value;
			}
		}
		foreach (PropertyDeclarationSyntax item2 in classDeclarationSyntax.Members.OfType<PropertyDeclarationSyntax>())
		{
			string value2 = item2.Type.ToString();
			dictionary[item2.Identifier.Text] = value2;
		}
		return dictionary;
	}
}
