using System.Collections.Generic;
using System.IO;

namespace Atomic.CodeGen.Core.Models;

public sealed class EntityAPIDefinition
{
	public required string SourceFile { get; init; }

	public required string Namespace { get; init; }

	public required string ClassName { get; init; }

	public required string Directory { get; init; }

	public string? TargetProject { get; init; }

	public string EntityType { get; init; } = "IEntity";

	public bool AggressiveInlining { get; init; } = true;

	public bool UnsafeAccess { get; init; } = true;

	public List<string> Imports { get; init; } = new List<string>();

	public List<string> Tags { get; init; } = new List<string>();

	public Dictionary<string, string> Values { get; init; } = new Dictionary<string, string>();

	public List<BehaviourDefinition> LinkedBehaviours { get; set; } = new List<BehaviourDefinition>();

	public List<string> Errors { get; init; } = new List<string>();

	public bool IsValid => Errors.Count == 0;

	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(ClassName))
		{
			Errors.Add("ClassName could not be determined.");
		}
		if (string.IsNullOrWhiteSpace(Directory))
		{
			Errors.Add("Directory could not be determined.");
		}
		if (string.IsNullOrWhiteSpace(EntityType))
		{
			Errors.Add("EntityType is required");
		}
		if (Tags.Count == 0 && Values.Count == 0)
		{
			Errors.Add("At least one Tag or Value must be defined");
		}
	}

	public string GetOutputFilePath(CodeGenConfig config)
	{
		return Path.Combine(Directory, ClassName + ".Generated.cs");
	}
}
