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
		string text = "/**\n";
		text += " * Code generation. Don't modify!\n";
		text = text + " * Generated from: " + Path.GetFileName(definition.SourceFile) + "\n";
		text = text + " * Source file path: " + definition.SourceFile + "\n";
		if (config.IncludeTimestamp)
		{
			text += $" * Generated at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n";
		}
		if (config.TrackOrphans)
		{
			text += " * AtomicGenerator: track file\n";
		}
		return text + " **/\n";
	}

	public static List<string> GetExpectedFilePaths(EntityDomainDefinition definition, string projectRoot)
	{
		List<string> list = new List<string>();
		string path = Path.Combine(projectRoot, definition.Directory);
		list.Add(Path.Combine(path, "I" + definition.EntityName + ".cs"));
		list.Add(Path.Combine(path, definition.EntityName + ".cs"));
		list.Add(Path.Combine(path, definition.EntityName + "Behaviours.cs"));
		if (definition.IsSceneEntityMode())
		{
			if (definition.GenerateProxy)
			{
				list.Add(Path.Combine(path, definition.EntityName + "Proxy.cs"));
			}
			if (definition.GenerateWorld)
			{
				list.Add(Path.Combine(path, definition.EntityName + "World.cs"));
			}
			if (definition.Pools.HasFlag(EntityPoolMode.SceneEntityPool))
			{
				list.Add(Path.Combine(path, definition.EntityName + "Pool.cs"));
			}
			if (definition.Pools.HasFlag(EntityPoolMode.PrefabEntityPool))
			{
				list.Add(Path.Combine(path, definition.EntityName + "PrefabPool.cs"));
			}
		}
		if (definition.IsPureEntityMode())
		{
			bool flag = definition.Factories.HasFlag(EntityFactoryMode.ScriptableEntityFactory) && definition.Factories.HasFlag(EntityFactoryMode.SceneEntityFactory);
			if (definition.Factories.HasFlag(EntityFactoryMode.ScriptableEntityFactory))
			{
				string text = (flag ? "Scriptable" : "");
				list.Add(Path.Combine(path, text + definition.EntityName + "Factory.cs"));
			}
			if (definition.Factories.HasFlag(EntityFactoryMode.SceneEntityFactory))
			{
				string text2 = (flag ? "Scene" : "");
				list.Add(Path.Combine(path, text2 + definition.EntityName + "Factory.cs"));
			}
			bool flag2 = definition.Bakers.HasFlag(EntityBakerMode.Standard) && definition.Bakers.HasFlag(EntityBakerMode.Optimized);
			if (definition.Bakers.HasFlag(EntityBakerMode.Standard))
			{
				list.Add(Path.Combine(path, definition.EntityName + "Baker.cs"));
			}
			if (definition.Bakers.HasFlag(EntityBakerMode.Optimized))
			{
				string text3 = (flag2 ? "Optimized" : "");
				list.Add(Path.Combine(path, definition.EntityName + "Baker" + text3 + ".cs"));
			}
			if (definition.Views.HasFlag(EntityViewMode.EntityView))
			{
				list.Add(Path.Combine(path, definition.EntityName + "View.cs"));
			}
			if (definition.Views.HasFlag(EntityViewMode.EntityViewCatalog))
			{
				list.Add(Path.Combine(path, definition.EntityName + "ViewCatalog.cs"));
			}
			if (definition.Views.HasFlag(EntityViewMode.EntityViewPool))
			{
				list.Add(Path.Combine(path, definition.EntityName + "ViewPool.cs"));
			}
			if (definition.Views.HasFlag(EntityViewMode.EntityCollectionView))
			{
				list.Add(Path.Combine(path, definition.EntityName + "CollectionView.cs"));
			}
		}
		if (definition.Installers.HasFlag(EntityInstallerMode.IEntityInstaller))
		{
			list.Add(Path.Combine(path, "I" + definition.EntityName + "Installer.cs"));
		}
		bool flag3 = definition.Installers.HasFlag(EntityInstallerMode.ScriptableEntityInstaller) && definition.Installers.HasFlag(EntityInstallerMode.SceneEntityInstaller);
		if (definition.Installers.HasFlag(EntityInstallerMode.ScriptableEntityInstaller))
		{
			string text4 = (flag3 ? "Scriptable" : "");
			list.Add(Path.Combine(path, text4 + definition.EntityName + "Installer.cs"));
		}
		if (definition.Installers.HasFlag(EntityInstallerMode.SceneEntityInstaller))
		{
			string text5 = (flag3 ? "Scene" : "");
			list.Add(Path.Combine(path, text5 + definition.EntityName + "Installer.cs"));
		}
		bool flag4 = definition.Aspects.HasFlag(EntityAspectMode.ScriptableEntityAspect) && definition.Aspects.HasFlag(EntityAspectMode.SceneEntityAspect);
		if (definition.Aspects.HasFlag(EntityAspectMode.ScriptableEntityAspect))
		{
			string text6 = (flag4 ? "Scriptable" : "");
			list.Add(Path.Combine(path, text6 + definition.EntityName + "Aspect.cs"));
		}
		if (definition.Aspects.HasFlag(EntityAspectMode.SceneEntityAspect))
		{
			string text7 = (flag4 ? "Scene" : "");
			list.Add(Path.Combine(path, text7 + definition.EntityName + "Aspect.cs"));
		}
		return list;
	}

	public static async Task GenerateMetaFileAsync(string csFilePath)
	{
		string metaPath = csFilePath + ".meta";
		string text = GenerateGuidFromPath(csFilePath);
		string contents = "fileFormatVersion: 2\r\nguid: " + text + "\r\nMonoImporter:\r\n  externalObjects: {}\r\n  serializedVersion: 2\r\n  defaultReferences: []\r\n  executionOrder: 0\r\n  icon: {instanceID: 0}\r\n  userData:\r\n  assetBundleName:\r\n  assetBundleVariant:\r\n";
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
		List<string> list = new List<string>();
		if (!string.IsNullOrWhiteSpace(definition.TargetProject))
		{
			list.Add(definition.TargetProject);
		}
		else
		{
			List<string> list2 = ProjectDetector.FindProjectsForFile(definition.SourceFile, config.GetAbsoluteProjectRoot());
			if (list2.Count == 0)
			{
				Logger.LogWarning("Could not find any .csproj containing " + definition.SourceFile + ". Generated file will not be linked to any project.");
			}
			else
			{
				list.AddRange(list2);
			}
		}
		foreach (string item in list)
		{
			await ProjectFileManager.AddGeneratedFileAsync(Path.Combine(config.GetAbsoluteProjectRoot(), item), generatedFilePath, config.GetAbsoluteProjectRoot());
		}
	}
}
