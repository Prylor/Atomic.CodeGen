using System;

namespace Atomic.CodeGen.Core.Models.EntityDomain;

[Flags]
public enum EntityFactoryMode
{
	None = 0,
	ScriptableEntityFactory = 1,
	SceneEntityFactory = 2
}
