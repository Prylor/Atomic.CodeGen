using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Atomic.CodeGen.Roslyn;

public static class ImportExtractor
{
	public static List<string> Extract(CompilationUnitSyntax root, string[]? excludeImports = null)
	{
		List<string> imports = new List<string>();
		HashSet<string> excludedNamespaces = new HashSet<string>(excludeImports ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
		HashSet<string> alwaysExcludedNamespaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Atomic.Entities", "System.Runtime.CompilerServices", "UnityEditor" };
		SyntaxList<UsingDirectiveSyntax>.Enumerator enumerator = root.Usings.GetEnumerator();
		while (enumerator.MoveNext())
		{
			UsingDirectiveSyntax current = enumerator.Current;
			string namespaceName = current.Name?.ToString();
			if (!string.IsNullOrWhiteSpace(namespaceName) && !excludedNamespaces.Contains(namespaceName) && !alwaysExcludedNamespaces.Contains(namespaceName) && current.Alias == null && !(current.StaticKeyword.Text == "static"))
			{
				imports.Add(namespaceName);
			}
		}
		return imports.Distinct().ToList();
	}
}
