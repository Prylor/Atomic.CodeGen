using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Atomic.CodeGen.Roslyn;

public record SemanticUsage
{
	public required string FilePath { get; init; }

	public required int Line { get; init; }

	public required int Column { get; init; }

	public required int Length { get; init; }

	public required string SymbolName { get; init; }

	[CompilerGenerated]
	[SetsRequiredMembers]
	protected SemanticUsage(SemanticUsage original)
	{
		FilePath = original.FilePath;
		Line = original.Line;
		Column = original.Column;
		Length = original.Length;
		SymbolName = original.SymbolName;
	}

	public SemanticUsage()
	{
	}
}
