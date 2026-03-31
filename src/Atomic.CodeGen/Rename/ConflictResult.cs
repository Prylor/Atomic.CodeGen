using System.Collections.Generic;

namespace Atomic.CodeGen.Rename;

public sealed class ConflictResult
{
	public required bool HasConflict { get; init; }

	public List<string> Conflicts { get; init; } = new List<string>();
}
