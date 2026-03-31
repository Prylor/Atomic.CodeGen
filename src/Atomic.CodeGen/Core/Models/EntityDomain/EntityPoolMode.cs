using System;

namespace Atomic.CodeGen.Core.Models.EntityDomain;

[Flags]
public enum EntityPoolMode
{
	None = 0,
	SceneEntityPool = 1,
	PrefabEntityPool = 2
}
