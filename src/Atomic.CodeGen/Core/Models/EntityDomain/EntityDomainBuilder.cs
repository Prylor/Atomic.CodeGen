using System.Collections.Generic;

namespace Atomic.CodeGen.Core.Models.EntityDomain;

public abstract class EntityDomainBuilder
{
	private EntityMode _mode;

	private bool _generateProxy;

	private bool _generateWorld;

	private EntityInstallerMode _installers;

	private EntityAspectMode _aspects;

	private EntityPoolMode _pools;

	private EntityFactoryMode _factories;

	private EntityBakerMode _bakers;

	private EntityViewMode _views;

	private List<string> _excludeImports = new List<string>();

	private string? _targetProject;

	public abstract string EntityName { get; }

	public abstract string Namespace { get; }

	public abstract string Directory { get; }

	public abstract void Configure();

	protected void EntityMode()
	{
		_mode = Atomic.CodeGen.Core.Models.EntityDomain.EntityMode.Entity;
	}

	protected void EntitySingletonMode()
	{
		_mode = Atomic.CodeGen.Core.Models.EntityDomain.EntityMode.EntitySingleton;
	}

	protected void SceneEntityMode()
	{
		_mode = Atomic.CodeGen.Core.Models.EntityDomain.EntityMode.SceneEntity;
	}

	protected void SceneEntitySingletonMode()
	{
		_mode = Atomic.CodeGen.Core.Models.EntityDomain.EntityMode.SceneEntitySingleton;
	}

	protected void GenerateProxy()
	{
		_generateProxy = true;
	}

	protected void GenerateWorld()
	{
		_generateWorld = true;
	}

	protected void IEntityInstaller()
	{
		_installers |= EntityInstallerMode.IEntityInstaller;
	}

	protected void ScriptableEntityInstaller()
	{
		_installers |= EntityInstallerMode.ScriptableEntityInstaller;
	}

	protected void SceneEntityInstaller()
	{
		_installers |= EntityInstallerMode.SceneEntityInstaller;
	}

	protected void ScriptableEntityAspect()
	{
		_aspects |= EntityAspectMode.ScriptableEntityAspect;
	}

	protected void SceneEntityAspect()
	{
		_aspects |= EntityAspectMode.SceneEntityAspect;
	}

	protected void SceneEntityPool()
	{
		_pools |= EntityPoolMode.SceneEntityPool;
	}

	protected void PrefabEntityPool()
	{
		_pools |= EntityPoolMode.PrefabEntityPool;
	}

	protected void ScriptableEntityFactory()
	{
		_factories |= EntityFactoryMode.ScriptableEntityFactory;
	}

	protected void SceneEntityFactory()
	{
		_factories |= EntityFactoryMode.SceneEntityFactory;
	}

	protected void StandardBaker()
	{
		_bakers |= EntityBakerMode.Standard;
	}

	protected void OptimizedBaker()
	{
		_bakers |= EntityBakerMode.Optimized;
	}

	protected void EntityView()
	{
		_views |= EntityViewMode.EntityView;
	}

	protected void EntityViewCatalog()
	{
		_views |= EntityViewMode.EntityViewCatalog;
	}

	protected void EntityViewPool()
	{
		_views |= EntityViewMode.EntityViewPool;
	}

	protected void EntityCollectionView()
	{
		_views |= EntityViewMode.EntityCollectionView;
	}

	protected void ExcludeImports(params string[] namespaces)
	{
		_excludeImports.AddRange(namespaces);
	}

	protected void TargetProject(string projectPath)
	{
		_targetProject = projectPath;
	}

	internal EntityDomainDefinition Build(string sourceFile, string className, List<string> detectedImports)
	{
		Configure();
		return new EntityDomainDefinition
		{
			SourceFile = sourceFile,
			ClassName = className,
			DetectedImports = detectedImports,
			EntityName = EntityName,
			Namespace = Namespace,
			Directory = Directory,
			Mode = _mode,
			GenerateProxy = _generateProxy,
			GenerateWorld = _generateWorld,
			Installers = _installers,
			Aspects = _aspects,
			Pools = _pools,
			Factories = _factories,
			Bakers = _bakers,
			Views = _views,
			ExcludeImports = _excludeImports.ToArray(),
			TargetProject = (_targetProject ?? "")
		};
	}
}
