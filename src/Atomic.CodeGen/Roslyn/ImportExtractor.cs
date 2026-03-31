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
		foreach (UsingDirectiveSyntax usingDirective in root.Usings)
		{
			string namespaceName = usingDirective.Name?.ToString();
			if (!string.IsNullOrWhiteSpace(namespaceName) && !excludedNamespaces.Contains(namespaceName) && !alwaysExcludedNamespaces.Contains(namespaceName) && usingDirective.Alias == null && !(usingDirective.StaticKeyword.Text == "static"))
			{
				imports.Add(namespaceName);
			}
		}
		return imports.Distinct().ToList();
	}
}
