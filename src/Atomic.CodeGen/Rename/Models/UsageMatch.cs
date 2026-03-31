using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;

namespace Atomic.CodeGen.Rename.Models;

public sealed record UsageMatch
{
	public required string FilePath { get; init; }

	public required int Line { get; init; }

	public required int Column { get; init; }

	public required int Length { get; init; }

	public required string MatchedText { get; init; }

	public required string ReplacementText { get; init; }

	public string? LineContext { get; init; }

	public bool IsAmbiguous { get; init; }

	public List<string>? PossibleApis { get; init; }

	public string Category { get; init; } = "Unknown";

	public string GetRelativePath(string projectRoot)
	{
		if (FilePath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
		{
			return FilePath.Substring(projectRoot.Length).TrimStart(Path.DirectorySeparatorChar, '/');
		}
		return FilePath;
	}

	public override string ToString()
	{
		return $"{Path.GetFileName(FilePath)}:{Line}:{Column} - {MatchedText} → {ReplacementText}";
	}

	[CompilerGenerated]
	[SetsRequiredMembers]
	private UsageMatch(UsageMatch original)
	{
		FilePath = original.FilePath;
		Line = original.Line;
		Column = original.Column;
		Length = original.Length;
		MatchedText = original.MatchedText;
		ReplacementText = original.ReplacementText;
		LineContext = original.LineContext;
		IsAmbiguous = original.IsAmbiguous;
		PossibleApis = original.PossibleApis;
		Category = original.Category;
	}

	public UsageMatch()
	{
	}
}
