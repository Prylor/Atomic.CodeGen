using System;
using System.Collections.Generic;
using System.Linq;

namespace Atomic.CodeGen.Core.Models.EntityDomain;

public class EntityDomainDefinition
{
	public string SourceFile { get; set; } = "";

	public string ClassName { get; set; } = "";

	public List<string> DetectedImports { get; set; } = new List<string>();

	public string EntityName { get; set; } = "";

	public string Namespace { get; set; } = "";

	public string Directory { get; set; } = "";

	public EntityMode Mode { get; set; }

	public bool GenerateProxy { get; set; }

	public bool GenerateWorld { get; set; }

	public EntityInstallerMode Installers { get; set; }

	public EntityAspectMode Aspects { get; set; }

	public EntityPoolMode Pools { get; set; }

	public EntityFactoryMode Factories { get; set; }

	public EntityBakerMode Bakers { get; set; }

	public EntityViewMode Views { get; set; }

	public string[]? ExcludeImports { get; set; }

	public string? TargetProject { get; set; }

	public string[] GetImports()
	{
		HashSet<string> excluded = new HashSet<string>(ExcludeImports ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
		excluded.Add("Atomic.Entities");
		excluded.Add("System.Runtime.CompilerServices");
		excluded.Add("UnityEditor");
		return DetectedImports.Where((string import) => !excluded.Contains(import)).Distinct().ToArray();
	}

	public string GetInterfaceName()
	{
		return "I" + EntityName;
	}

	public bool IsSceneEntityMode()
	{
		EntityMode mode = Mode;
		if ((uint)(mode - 2) <= 1u)
		{
			return true;
		}
		return false;
	}

	public bool IsPureEntityMode()
	{
		EntityMode mode = Mode;
		if ((uint)mode <= 1u)
		{
			return true;
		}
		return false;
	}

	public string[] Validate()
	{
		List<string> list = new List<string>();
		if (string.IsNullOrWhiteSpace(EntityName))
		{
			list.Add("EntityName is required");
		}
		if (string.IsNullOrWhiteSpace(Namespace))
		{
			list.Add("Namespace is required");
		}
		if (string.IsNullOrWhiteSpace(Directory))
		{
			list.Add("Directory is required");
		}
		if (IsPureEntityMode())
		{
			if (GenerateProxy)
			{
				list.Add("GenerateProxy is only supported for SceneEntity modes");
			}
			if (GenerateWorld)
			{
				list.Add("GenerateWorld is only supported for SceneEntity modes");
			}
			if (Pools != EntityPoolMode.None)
			{
				list.Add("Pools are only supported for SceneEntity modes");
			}
		}
		if (IsSceneEntityMode())
		{
			if (Factories != EntityFactoryMode.None)
			{
				list.Add("Factories are only supported for pure Entity modes");
			}
			if (Bakers != EntityBakerMode.None)
			{
				list.Add("Bakers are only supported for pure Entity modes");
			}
			if (Views != EntityViewMode.None)
			{
				list.Add("Views are only supported for pure Entity modes");
			}
		}
		return list.ToArray();
	}
}
