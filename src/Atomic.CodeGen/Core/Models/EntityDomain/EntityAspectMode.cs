using System;

namespace Atomic.CodeGen.Core.Models.EntityDomain;

[Flags]
public enum EntityAspectMode
{
	None = 0,
	ScriptableEntityAspect = 1,
	SceneEntityAspect = 2
}
