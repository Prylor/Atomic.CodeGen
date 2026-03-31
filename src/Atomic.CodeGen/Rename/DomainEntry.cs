namespace Atomic.CodeGen.Rename;

public sealed class DomainEntry
{
	public required string ClassName { get; init; }

	public required string EntityName { get; init; }

	public required string Namespace { get; init; }

	public required string Directory { get; init; }

	public required string SourceFile { get; init; }

	public override string ToString()
	{
		return $"{ClassName} ({EntityName})";
	}
}
