namespace Atomic.CodeGen.Rename.Models;

public sealed class RenameOperation
{
	public required string FilePath { get; init; }

	public required int StartOffset { get; init; }

	public required int Length { get; init; }

	public required string OldText { get; init; }

	public required string NewText { get; init; }

	public int Line { get; init; }

	public int Column { get; init; }

	public static RenameOperation FromUsageMatch(UsageMatch match, string fileContent)
	{
		string[] array = fileContent.Split('\n');
		int num = 0;
		for (int i = 0; i < match.Line - 1 && i < array.Length; i++)
		{
			num += array[i].Length + 1;
		}
		num += match.Column - 1;
		return new RenameOperation
		{
			FilePath = match.FilePath,
			StartOffset = num,
			Length = match.Length,
			OldText = match.MatchedText,
			NewText = match.ReplacementText,
			Line = match.Line,
			Column = match.Column
		};
	}
}
