using System;

namespace Atomic.CodeGen.Core.Models.EntityDomain;

[Flags]
public enum EntityBakerMode
{
	None = 0,
	Standard = 1,
	Optimized = 2
}
