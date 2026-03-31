using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Atomic.CodeGen.Rename;

public sealed class ImportAnalyzer
{
	private readonly Dictionary<string, FileImports> _cache = new Dictionary<string, FileImports>(StringComparer.OrdinalIgnoreCase);

	public FileImports GetImports(string filePath)
	{
		if (_cache.TryGetValue(filePath, out FileImports cachedImports))
		{
			return cachedImports;
		}
		FileImports fileImports = ParseImports(filePath);
		_cache[filePath] = fileImports;
		return fileImports;
	}

	private static FileImports ParseImports(string filePath)
	{
		if (!File.Exists(filePath))
		{
			return FileImports.Empty;
		}
		return ParseImportsFromSource(File.ReadAllText(filePath), filePath);
	}

	public static FileImports ParseImportsFromSource(string sourceCode, string? filePath = null)
	{
		SyntaxNode root = CSharpSyntaxTree.ParseText(sourceCode).GetRoot();
		FileImports fileImports = new FileImports
		{
			FilePath = (filePath ?? string.Empty)
		};
		FileScopedNamespaceDeclarationSyntax fileScopedNamespaceDeclarationSyntax = root.DescendantNodes().OfType<FileScopedNamespaceDeclarationSyntax>().FirstOrDefault();
		if (fileScopedNamespaceDeclarationSyntax != null)
		{
			fileImports.FileNamespace = fileScopedNamespaceDeclarationSyntax.Name.ToString();
		}
		else
		{
			NamespaceDeclarationSyntax namespaceDeclarationSyntax = root.DescendantNodes().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
			if (namespaceDeclarationSyntax != null)
			{
				fileImports.FileNamespace = namespaceDeclarationSyntax.Name.ToString();
			}
		}
		if (root is CompilationUnitSyntax compilationUnitSyntax)
		{
			SyntaxList<UsingDirectiveSyntax>.Enumerator enumerator = compilationUnitSyntax.Usings.GetEnumerator();
			while (enumerator.MoveNext())
			{
				UsingDirectiveSyntax current = enumerator.Current;
				if (current.StaticKeyword.IsKind(SyntaxKind.StaticKeyword))
				{
					fileImports.StaticImports.Add(current.Name?.ToString() ?? string.Empty);
				}
				else if (current.Alias != null)
				{
					string aliasName = current.Alias.Name.ToString();
					string aliasTarget = current.Name?.ToString() ?? string.Empty;
					fileImports.Aliases[aliasName] = aliasTarget;
				}
				else
				{
					fileImports.Namespaces.Add(current.Name?.ToString() ?? string.Empty);
				}
			}
		}
		foreach (BaseNamespaceDeclarationSyntax namespaceDecl in root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>())
		{
			SyntaxList<UsingDirectiveSyntax>.Enumerator enumerator = namespaceDecl.Usings.GetEnumerator();
			while (enumerator.MoveNext())
			{
				UsingDirectiveSyntax current2 = enumerator.Current;
				if (current2.StaticKeyword.IsKind(SyntaxKind.StaticKeyword))
				{
					fileImports.StaticImports.Add(current2.Name?.ToString() ?? string.Empty);
				}
				else if (current2.Alias != null)
				{
					string nsAliasName = current2.Alias.Name.ToString();
					string nsAliasTarget = current2.Name?.ToString() ?? string.Empty;
					fileImports.Aliases[nsAliasName] = nsAliasTarget;
				}
				else
				{
					fileImports.Namespaces.Add(current2.Name?.ToString() ?? string.Empty);
				}
			}
		}
		if (!string.IsNullOrEmpty(fileImports.FileNamespace))
		{
			fileImports.Namespaces.Add(fileImports.FileNamespace);
		}
		return fileImports;
	}

	public void ClearCache()
	{
		_cache.Clear();
	}

	public void InvalidateFile(string filePath)
	{
		_cache.Remove(filePath);
	}
}
