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
		List<string> list = new List<string>();
		HashSet<string> hashSet = new HashSet<string>(excludeImports ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
		HashSet<string> hashSet2 = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Atomic.Entities", "System.Runtime.CompilerServices", "UnityEditor" };
		SyntaxList<UsingDirectiveSyntax>.Enumerator enumerator = root.Usings.GetEnumerator();
		while (enumerator.MoveNext())
		{
			UsingDirectiveSyntax current = enumerator.Current;
			string text = current.Name?.ToString();
			if (!string.IsNullOrWhiteSpace(text) && !hashSet.Contains(text) && !hashSet2.Contains(text) && current.Alias == null && !(current.StaticKeyword.Text == "static"))
			{
				list.Add(text);
			}
		}
		return list.Distinct().ToList();
	}
}
