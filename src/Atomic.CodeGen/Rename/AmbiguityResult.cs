using System.Collections.Generic;

namespace Atomic.CodeGen.Rename;

public sealed class AmbiguityResult
{
	public required bool IsAmbiguous { get; init; }

	public required List<ApiEntry> MatchingApis { get; init; }
}
