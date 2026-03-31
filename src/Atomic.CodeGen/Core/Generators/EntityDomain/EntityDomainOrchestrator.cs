using System;
using System.IO;
using System.Threading.Tasks;
using Atomic.CodeGen.Core.Models;
using Atomic.CodeGen.Core.Models.EntityDomain;
using Atomic.CodeGen.Utils;

namespace Atomic.CodeGen.Core.Generators.EntityDomain;

public class EntityDomainOrchestrator
{
	private readonly EntityDomainDefinition _definition;

	private readonly CodeGenConfig _config;

	public EntityDomainOrchestrator(EntityDomainDefinition definition, CodeGenConfig config)
	{
		_definition = definition;
		_config = config;
	}

	public async Task<bool> GenerateAsync()
	{
		try
		{
			string[] array = _definition.Validate();
			if (array.Length != 0)
			{
				Logger.LogError("Invalid EntityDomain definition in " + _definition.SourceFile + ":");
				string[] array2 = array;
				foreach (string text in array2)
				{
					Logger.LogError("  - " + text);
				}
				return false;
			}
			string absoluteProjectRoot = _config.GetAbsoluteProjectRoot();
			string outputDir = Path.Combine(absoluteProjectRoot, _definition.Directory);
			if (!Directory.Exists(outputDir))
			{
				Directory.CreateDirectory(outputDir);
				Logger.LogVerbose("Created directory: " + outputDir);
			}
			Logger.LogInfo("Generating EntityDomain for: " + _definition.EntityName);
			Logger.LogVerbose($"  Mode: {_definition.Mode}");
			Logger.LogVerbose("  Output: " + _definition.Directory);
			await GenerateCoreFiles(outputDir);
			if (_definition.IsSceneEntityMode())
			{
				if (_definition.GenerateProxy)
				{
					await GenerateProxy(outputDir);
				}
				if (_definition.GenerateWorld)
				{
					await GenerateWorld(outputDir);
				}
				if (_definition.Pools != EntityPoolMode.None)
				{
					await GeneratePools(outputDir);
				}
			}
			if (_definition.IsPureEntityMode())
			{
				if (_definition.Factories != EntityFactoryMode.None)
				{
					await GenerateFactories(outputDir);
				}
				if (_definition.Bakers != EntityBakerMode.None)
				{
					await GenerateBakers(outputDir);
				}
				if (_definition.Views != EntityViewMode.None)
				{
					await GenerateViews(outputDir);
				}
			}
			if (_definition.Installers != EntityInstallerMode.None)
			{
				await GenerateInstallers(outputDir);
			}
			if (_definition.Aspects != EntityAspectMode.None)
			{
				await GenerateAspects(outputDir);
			}
			Logger.LogSuccess("Generated EntityDomain: " + _definition.EntityName);
			return true;
		}
		catch (Exception ex)
		{
			Logger.LogError("Failed to generate EntityDomain " + _definition.EntityName + ": " + ex.Message);
			return false;
		}
	}

	private async Task GenerateCoreFiles(string outputDir)
	{
		await EntityInterfaceGenerator.GenerateAsync(_definition, _config, outputDir);
		await EntityConcreteGenerator.GenerateAsync(_definition, _config, outputDir);
		await EntityBehaviourGenerator.GenerateAsync(_definition, _config, outputDir);
	}

	private async Task GenerateProxy(string outputDir)
	{
		await EntityProxyGenerator.GenerateAsync(_definition, _config, outputDir);
	}

	private async Task GenerateWorld(string outputDir)
	{
		await EntityWorldGenerator.GenerateAsync(_definition, _config, outputDir);
	}

	private async Task GeneratePools(string outputDir)
	{
		if (_definition.Pools.HasFlag(EntityPoolMode.SceneEntityPool))
		{
			await EntityPoolGenerators.GenerateScenePoolAsync(_definition, _config, outputDir);
		}
		if (_definition.Pools.HasFlag(EntityPoolMode.PrefabEntityPool))
		{
			await EntityPoolGenerators.GeneratePrefabPoolAsync(_definition, _config, outputDir);
		}
	}

	private async Task GenerateFactories(string outputDir)
	{
		if (_definition.Factories.HasFlag(EntityFactoryMode.ScriptableEntityFactory))
		{
			await EntityFactoryGenerators.GenerateScriptableFactoryAsync(_definition, _config, outputDir);
		}
		if (_definition.Factories.HasFlag(EntityFactoryMode.SceneEntityFactory))
		{
			await EntityFactoryGenerators.GenerateSceneFactoryAsync(_definition, _config, outputDir);
		}
	}

	private async Task GenerateBakers(string outputDir)
	{
		if (_definition.Bakers.HasFlag(EntityBakerMode.Standard))
		{
			await EntityBakerGenerators.GenerateStandardBakerAsync(_definition, _config, outputDir);
		}
		if (_definition.Bakers.HasFlag(EntityBakerMode.Optimized))
		{
			await EntityBakerGenerators.GenerateOptimizedBakerAsync(_definition, _config, outputDir);
		}
	}

	private async Task GenerateViews(string outputDir)
	{
		if (_definition.Views.HasFlag(EntityViewMode.EntityView))
		{
			await EntityViewGenerators.GenerateEntityViewAsync(_definition, _config, outputDir);
		}
		if (_definition.Views.HasFlag(EntityViewMode.EntityViewCatalog))
		{
			await EntityViewGenerators.GenerateEntityViewCatalogAsync(_definition, _config, outputDir);
		}
		if (_definition.Views.HasFlag(EntityViewMode.EntityViewPool))
		{
			await EntityViewGenerators.GenerateEntityViewPoolAsync(_definition, _config, outputDir);
		}
		if (_definition.Views.HasFlag(EntityViewMode.EntityCollectionView))
		{
			await EntityViewGenerators.GenerateEntityCollectionViewAsync(_definition, _config, outputDir);
		}
	}

	private async Task GenerateInstallers(string outputDir)
	{
		if (_definition.Installers.HasFlag(EntityInstallerMode.IEntityInstaller))
		{
			await EntityInstallerGenerators.GenerateInstallerInterfaceAsync(_definition, _config, outputDir);
		}
		if (_definition.Installers.HasFlag(EntityInstallerMode.ScriptableEntityInstaller))
		{
			await EntityInstallerGenerators.GenerateScriptableInstallerAsync(_definition, _config, outputDir);
		}
		if (_definition.Installers.HasFlag(EntityInstallerMode.SceneEntityInstaller))
		{
			await EntityInstallerGenerators.GenerateSceneInstallerAsync(_definition, _config, outputDir);
		}
	}

	private async Task GenerateAspects(string outputDir)
	{
		if (_definition.Aspects.HasFlag(EntityAspectMode.ScriptableEntityAspect))
		{
			await EntityAspectGenerators.GenerateScriptableAspectAsync(_definition, _config, outputDir);
		}
		if (_definition.Aspects.HasFlag(EntityAspectMode.SceneEntityAspect))
		{
			await EntityAspectGenerators.GenerateSceneAspectAsync(_definition, _config, outputDir);
		}
	}
}
