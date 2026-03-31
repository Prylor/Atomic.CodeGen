using System;
using System.Collections.Generic;

namespace Atomic.CodeGen.Rename;

public sealed class FileImports
{
	public static readonly FileImports Empty = new FileImports();

	public string FilePath { get; init; } = string.Empty;

	public string FileNamespace { get; set; } = string.Empty;

	public HashSet<string> Namespaces { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	public HashSet<string> StaticImports { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	public Dictionary<string, string> Aliases { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

	public IEnumerable<string> AllAccessibleNamespaces => Namespaces;

	public bool HasNamespaceImport(string ns)
	{
		return Namespaces.Contains(ns);
	}

	public bool HasStaticImport(string fullTypeName)
	{
		return StaticImports.Contains(fullTypeName);
	}

	public string? ResolveAlias(string alias)
	{
		return Aliases.GetValueOrDefault(alias);
	}

	public bool CouldReferTo(string typeReference, string fullTypeName)
	{
		if (typeReference.Equals(fullTypeName, StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		int num = fullTypeName.LastIndexOf('.');
		if (num < 0)
		{
			return typeReference.Equals(fullTypeName, StringComparison.OrdinalIgnoreCase);
		}
		string ns = fullTypeName.Substring(0, num);
		string value = fullTypeName.Substring(num + 1);
		if (typeReference.Equals(value, StringComparison.OrdinalIgnoreCase) && HasNamespaceImport(ns))
		{
			return true;
		}
		int num2 = typeReference.IndexOf('.');
		if (num2 > 0)
		{
			string alias = typeReference.Substring(0, num2);
			string text = typeReference.Substring(num2 + 1);
			string text2 = ResolveAlias(alias);
			if (text2 != null && (text2 + "." + text).Equals(fullTypeName, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		return false;
	}
}
