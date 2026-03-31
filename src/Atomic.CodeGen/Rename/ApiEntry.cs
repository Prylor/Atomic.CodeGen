using System.Collections.Generic;
using System.Linq;
using Atomic.CodeGen.Core.Models;

namespace Atomic.CodeGen.Rename;

public sealed class ApiEntry
{
	public required string ClassName { get; init; }

	public required string Namespace { get; init; }

	public required string FullName { get; init; }

	public required string SourceFile { get; init; }

	public required string EntityType { get; init; }

	public required HashSet<string> Tags { get; init; }

	public required HashSet<string> Values { get; init; }

	public required HashSet<string> Behaviours { get; init; }

	public List<BehaviourDefinition> BehaviourDefinitions { get; init; } = new List<BehaviourDefinition>();

	public IEnumerable<string> AllSymbols => Tags.Concat(Values).Concat(Behaviours);

	public override string ToString()
	{
		return FullName;
	}
}
