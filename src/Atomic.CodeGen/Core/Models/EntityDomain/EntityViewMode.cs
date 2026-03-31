using System;

namespace Atomic.CodeGen.Core.Models.EntityDomain;

[Flags]
public enum EntityViewMode
{
	None = 0,
	EntityView = 1,
	EntityViewCatalog = 2,
	EntityViewPool = 4,
	EntityCollectionView = 8
}
