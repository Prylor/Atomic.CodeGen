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
		int lastDotIndex = fullTypeName.LastIndexOf('.');
		if (lastDotIndex < 0)
		{
			return typeReference.Equals(fullTypeName, StringComparison.OrdinalIgnoreCase);
		}
		string ns = fullTypeName.Substring(0, lastDotIndex);
		string shortName = fullTypeName.Substring(lastDotIndex + 1);
		if (typeReference.Equals(shortName, StringComparison.OrdinalIgnoreCase) && HasNamespaceImport(ns))
		{
			return true;
		}
		int dotIndex = typeReference.IndexOf('.');
		if (dotIndex > 0)
		{
			string alias = typeReference.Substring(0, dotIndex);
			string remainder = typeReference.Substring(dotIndex + 1);
			string resolvedNamespace = ResolveAlias(alias);
			if (resolvedNamespace != null && (resolvedNamespace + "." + remainder).Equals(fullTypeName, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		return false;
	}
}
