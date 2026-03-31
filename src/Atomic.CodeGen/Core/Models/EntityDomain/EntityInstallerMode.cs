using System;

namespace Atomic.CodeGen.Core.Models.EntityDomain;

[Flags]
public enum EntityInstallerMode
{
	None = 0,
	IEntityInstaller = 1,
	ScriptableEntityInstaller = 2,
	SceneEntityInstaller = 4
}
