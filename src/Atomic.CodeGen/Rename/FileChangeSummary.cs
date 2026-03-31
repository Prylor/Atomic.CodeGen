using System.Collections.Generic;
using Atomic.CodeGen.Rename.Models;

namespace Atomic.CodeGen.Rename;

public sealed class FileChangeSummary
{
	public required string FilePath { get; init; }

	public required int ChangeCount { get; init; }

	public required int AmbiguousCount { get; init; }

	public required List<string> Categories { get; init; }

	public required List<UsageMatch> Usages { get; init; }
}
