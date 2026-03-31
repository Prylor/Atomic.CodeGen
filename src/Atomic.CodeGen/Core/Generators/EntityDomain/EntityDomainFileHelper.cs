using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Atomic.CodeGen.Core.Models;
using Atomic.CodeGen.Core.Models.EntityDomain;
using Atomic.CodeGen.Utils;

namespace Atomic.CodeGen.Core.Generators.EntityDomain;

public static class EntityDomainFileHelper
{
	public static string GetFileHeader(EntityDomainDefinition definition, string fileName, CodeGenConfig config)
	{
		string header = "/**\n";
		header += " * Code generation. Don't modify!\n";
		header = header + " * Generated from: " + Path.GetFileName(definition.SourceFile) + "\n";
		header = header + " * Source file path: " + definition.SourceFile + "\n";
		if (config.IncludeTimestamp)
		{
			header += $" * Generated at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n";
		}
		if (config.TrackOrphans)
		{
			header += " * AtomicGenerator: track file\n";
		}
		return header + " **/\n";
	}

	public static List<string> GetExpectedFilePaths(EntityDomainDefinition definition, string projectRoot)
	{
		List<string> expectedFiles = new List<string>();
		string path = Path.Combine(projectRoot, definition.Directory);
		expectedFiles.Add(Path.Combine(path, "I" + definition.EntityName + ".cs"));
		expectedFiles.Add(Path.Combine(path, definition.EntityName + ".cs"));
		expectedFiles.Add(Path.Combine(path, definition.EntityName + "Behaviours.cs"));
		if (definition.IsSceneEntityMode())
		{
			if (definition.GenerateProxy)
			{
				expectedFiles.Add(Path.Combine(path, definition.EntityName + "Proxy.cs"));
			}
			if (definition.GenerateWorld)
			{
				expectedFiles.Add(Path.Combine(path, definition.EntityName + "World.cs"));
			}
			if (definition.Pools.HasFlag(EntityPoolMode.SceneEntityPool))
			{
				expectedFiles.Add(Path.Combine(path, definition.EntityName + "Pool.cs"));
			}
			if (definition.Pools.HasFlag(EntityPoolMode.PrefabEntityPool))
			{
				expectedFiles.Add(Path.Combine(path, definition.EntityName + "PrefabPool.cs"));
			}
		}
		if (definition.IsPureEntityMode())
		{
			bool hasBothFactories = definition.Factories.HasFlag(EntityFactoryMode.ScriptableEntityFactory) && definition.Factories.HasFlag(EntityFactoryMode.SceneEntityFactory);
			if (definition.Factories.HasFlag(EntityFactoryMode.ScriptableEntityFactory))
			{
				string factoryPrefix = (hasBothFactories ? "Scriptable" : "");
				expectedFiles.Add(Path.Combine(path, factoryPrefix + definition.EntityName + "Factory.cs"));
			}
			if (definition.Factories.HasFlag(EntityFactoryMode.SceneEntityFactory))
			{
				string factoryPrefix = (hasBothFactories ? "Scene" : "");
				expectedFiles.Add(Path.Combine(path, factoryPrefix + definition.EntityName + "Factory.cs"));
			}
			bool hasBothBakers = definition.Bakers.HasFlag(EntityBakerMode.Standard) && definition.Bakers.HasFlag(EntityBakerMode.Optimized);
			if (definition.Bakers.HasFlag(EntityBakerMode.Standard))
			{
				expectedFiles.Add(Path.Combine(path, definition.EntityName + "Baker.cs"));
			}
			if (definition.Bakers.HasFlag(EntityBakerMode.Optimized))
			{
				string bakerSuffix = (hasBothBakers ? "Optimized" : "");
				expectedFiles.Add(Path.Combine(path, definition.EntityName + "Baker" + bakerSuffix + ".cs"));
			}
			if (definition.Views.HasFlag(EntityViewMode.EntityView))
			{
				expectedFiles.Add(Path.Combine(path, definition.EntityName + "View.cs"));
			}
			if (definition.Views.HasFlag(EntityViewMode.EntityViewCatalog))
			{
				expectedFiles.Add(Path.Combine(path, definition.EntityName + "ViewCatalog.cs"));
			}
			if (definition.Views.HasFlag(EntityViewMode.EntityViewPool))
			{
				expectedFiles.Add(Path.Combine(path, definition.EntityName + "ViewPool.cs"));
			}
			if (definition.Views.HasFlag(EntityViewMode.EntityCollectionView))
			{
				expectedFiles.Add(Path.Combine(path, definition.EntityName + "CollectionView.cs"));
			}
		}
		if (definition.Installers.HasFlag(EntityInstallerMode.IEntityInstaller))
		{
			expectedFiles.Add(Path.Combine(path, "I" + definition.EntityName + "Installer.cs"));
		}
		bool hasBothInstallers = definition.Installers.HasFlag(EntityInstallerMode.ScriptableEntityInstaller) && definition.Installers.HasFlag(EntityInstallerMode.SceneEntityInstaller);
		if (definition.Installers.HasFlag(EntityInstallerMode.ScriptableEntityInstaller))
		{
			string installerPrefix = (hasBothInstallers ? "Scriptable" : "");
			expectedFiles.Add(Path.Combine(path, installerPrefix + definition.EntityName + "Installer.cs"));
		}
		if (definition.Installers.HasFlag(EntityInstallerMode.SceneEntityInstaller))
		{
			string installerPrefix = (hasBothInstallers ? "Scene" : "");
			expectedFiles.Add(Path.Combine(path, installerPrefix + definition.EntityName + "Installer.cs"));
		}
		bool hasBothAspects = definition.Aspects.HasFlag(EntityAspectMode.ScriptableEntityAspect) && definition.Aspects.HasFlag(EntityAspectMode.SceneEntityAspect);
		if (definition.Aspects.HasFlag(EntityAspectMode.ScriptableEntityAspect))
		{
			string aspectPrefix = (hasBothAspects ? "Scriptable" : "");
			expectedFiles.Add(Path.Combine(path, aspectPrefix + definition.EntityName + "Aspect.cs"));
		}
		if (definition.Aspects.HasFlag(EntityAspectMode.SceneEntityAspect))
		{
			string aspectPrefix = (hasBothAspects ? "Scene" : "");
			expectedFiles.Add(Path.Combine(path, aspectPrefix + definition.EntityName + "Aspect.cs"));
		}
		return expectedFiles;
	}

	public static async Task GenerateMetaFileAsync(string csFilePath)
	{
		string metaPath = csFilePath + ".meta";
		string guid = GenerateGuidFromPath(csFilePath);
		string contents = "fileFormatVersion: 2\r\nguid: " + guid + "\r\nMonoImporter:\r\n  externalObjects: {}\r\n  serializedVersion: 2\r\n  defaultReferences: []\r\n  executionOrder: 0\r\n  icon: {instanceID: 0}\r\n  userData:\r\n  assetBundleName:\r\n  assetBundleVariant:\r\n";
		await File.WriteAllTextAsync(metaPath, contents);
		Logger.LogVerbose("Generated meta file: " + metaPath);
	}

	private static string GenerateGuidFromPath(string path)
	{
		using MD5 mD = MD5.Create();
		byte[] b = mD.ComputeHash(Encoding.UTF8.GetBytes(path));
		return new Guid(b).ToString("N");
	}

	public static async Task LinkToProjectsAsync(EntityDomainDefinition definition, CodeGenConfig config, string generatedFilePath)
	{
		List<string> targetProjects = new List<string>();
		if (!string.IsNullOrWhiteSpace(definition.TargetProject))
		{
			targetProjects.Add(definition.TargetProject);
		}
		else
		{
			List<string> detectedProjects = ProjectDetector.FindProjectsForFile(definition.SourceFile, config.GetAbsoluteProjectRoot());
			if (detectedProjects.Count == 0)
			{
				Logger.LogWarning("Could not find any .csproj containing " + definition.SourceFile + ". Generated file will not be linked to any project.");
			}
			else
			{
				targetProjects.AddRange(detectedProjects);
			}
		}
		foreach (string projectPath in targetProjects)
		{
			await ProjectFileManager.AddGeneratedFileAsync(Path.Combine(config.GetAbsoluteProjectRoot(), projectPath), generatedFilePath, config.GetAbsoluteProjectRoot());
		}
	}
}
