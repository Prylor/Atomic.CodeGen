using System.Collections.Generic;
using System.Linq;

namespace Atomic.CodeGen.Rename.Models;

public sealed class RenameContext
{
	public required RenameType Type { get; init; }

	public required string OldName { get; init; }

	public required string NewName { get; init; }

	public required string OwnerName { get; init; }

	public string OwnerNamespace { get; set; } = string.Empty;

	public string SourceFilePath { get; set; } = string.Empty;

	public string OutputDirectory { get; set; } = string.Empty;

	public int SourceLine { get; init; }

	public int SourceColumn { get; init; }

	public bool RenameSourceFile { get; set; }

	public List<(string OldPath, string NewPath)> FileRenames { get; set; } = new List<(string, string)>();

	public List<UsageMatch> Usages { get; set; } = new List<UsageMatch>();

	public List<UsageMatch> AmbiguousUsages => Usages.Where((UsageMatch u) => u.IsAmbiguous).ToList();

	public List<UsageMatch> CertainUsages => Usages.Where((UsageMatch u) => !u.IsAmbiguous).ToList();

	public List<string> Errors { get; set; } = new List<string>();

	public List<string> Warnings { get; set; } = new List<string>();

	public bool UsedSemanticAnalysis { get; set; }

	public bool IsValid => Errors.Count == 0;

	public IEnumerable<string> AffectedFiles => Usages
		.Select(u => u.FilePath)
		.Concat(FileRenames.Select(r => r.OldPath))
		.Append(SourceFilePath)
		.Distinct();

	public Dictionary<string, List<UsageMatch>> UsagesByFile => Usages
		.GroupBy(u => u.FilePath)
		.ToDictionary(
			g => g.Key,
			g => g.OrderBy(u => u.Line).ThenBy(u => u.Column).ToList());

	public string GetSummary()
	{
		int fileCount = AffectedFiles.Count();
		int usageCount = Usages.Count;
		int ambiguousCount = AmbiguousUsages.Count;
		string summary = $"{Type} rename: {OldName} → {NewName}\n";
		summary += $"Owner: {OwnerNamespace}.{OwnerName}\n";
		summary += $"Usages: {usageCount} in {fileCount} file(s)";
		if (ambiguousCount > 0)
		{
			summary += $" ({ambiguousCount} ambiguous)";
		}
		return summary;
	}
}
