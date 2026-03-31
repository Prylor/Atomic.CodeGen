using System.Collections.Generic;

namespace Atomic.CodeGen.Core.Models;

public sealed class BehaviourDefinition
{
	public required string SourceFile { get; init; }

	public required string LinkedApiTypeName { get; init; }

	public required string Namespace { get; init; }

	public required string ClassName { get; init; }

	public List<(string Name, string Type)> ConstructorParameters { get; init; } = new List<(string, string)>();

	public List<string> RequiredImports { get; init; } = new List<string>();

	public List<string> Errors { get; init; } = new List<string>();

	public bool IsValid => Errors.Count == 0;

	public string FullTypeName
	{
		get
		{
			if (!string.IsNullOrEmpty(Namespace))
			{
				return Namespace + "." + ClassName;
			}
			return ClassName;
		}
	}
}
