using System.Collections.Generic;
using Atomic.CodeGen.Core.Models;
using Atomic.CodeGen.Core.Models.EntityDomain;

namespace Atomic.CodeGen.Roslyn;

public sealed class DiscoveryResult
{
	public List<EntityAPIDefinition> EntityApis { get; } = new List<EntityAPIDefinition>();

	public List<BehaviourDefinition> Behaviours { get; } = new List<BehaviourDefinition>();

	public Dictionary<string, EntityDomainDefinition> Domains { get; } = new Dictionary<string, EntityDomainDefinition>();
}
