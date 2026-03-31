using System.IO;
using System.Text;
using System.Threading.Tasks;
using Atomic.CodeGen.Core.Models;
using Atomic.CodeGen.Core.Models.EntityDomain;
using Atomic.CodeGen.Utils;

namespace Atomic.CodeGen.Core.Generators.EntityDomain;

public static class EntityPoolGenerators
{
	public static async Task GenerateScenePoolAsync(EntityDomainDefinition definition, CodeGenConfig config, string outputDir)
	{
		string fileName = definition.EntityName + "Pool.cs";
		string filePath = Path.Combine(outputDir, fileName);
		string contents = GenerateScenePoolContent(definition, config, fileName);
		await File.WriteAllTextAsync(filePath, contents);
		await EntityDomainFileHelper.GenerateMetaFileAsync(filePath);
		await EntityDomainFileHelper.LinkToProjectsAsync(definition, config, filePath);
		Logger.LogVerbose("Generated: " + fileName);
	}

	public static async Task GeneratePrefabPoolAsync(EntityDomainDefinition definition, CodeGenConfig config, string outputDir)
	{
		string fileName = definition.EntityName + "PrefabPool.cs";
		string filePath = Path.Combine(outputDir, fileName);
		string contents = GeneratePrefabPoolContent(definition, config, fileName);
		await File.WriteAllTextAsync(filePath, contents);
		await EntityDomainFileHelper.GenerateMetaFileAsync(filePath);
		await EntityDomainFileHelper.LinkToProjectsAsync(definition, config, filePath);
		Logger.LogVerbose("Generated: " + fileName);
	}

	private static string GenerateScenePoolContent(EntityDomainDefinition definition, CodeGenConfig config, string fileName)
	{
		StringBuilder sb = new StringBuilder();
		sb.Append(EntityDomainFileHelper.GetFileHeader(definition, fileName, config));
		sb.AppendLine();
		sb.AppendLine("using Atomic.Entities;");
		string[] imports = definition.GetImports();
		foreach (string text in imports)
		{
			if (!string.IsNullOrWhiteSpace(text))
			{
				string text2 = text.Trim();
				sb.AppendLine(text2.StartsWith("using") ? text2 : ("using " + text2 + ";"));
			}
		}
		sb.AppendLine();
		sb.AppendLine($"namespace {definition.Namespace}");
		sb.AppendLine("{");
		sb.AppendLine("    /// <summary>");
		sb.AppendLine($"    /// A Unity-integrated pool for <see cref=\"{definition.EntityName}\"/> entities that exist within a scene.");
		sb.AppendLine("    /// </summary>");
		sb.AppendLine("    /// <remarks>");
		sb.AppendLine($"    /// Implements <see cref=\"IEntityPool{{I{definition.EntityName}}}\"/> for renting and returning scene-based entities.");
		sb.AppendLine("    /// </remarks>");
		sb.AppendLine($"    public sealed class {definition.EntityName}Pool : SceneEntityPool<{definition.EntityName}>, IEntityPool<I{definition.EntityName}>");
		sb.AppendLine("    {");
		sb.AppendLine($"        I{definition.EntityName} IEntityPool<I{definition.EntityName}>.Rent() => this.Rent();");
		sb.AppendLine();
		sb.AppendLine($"        void IEntityPool<I{definition.EntityName}>.Return(I{definition.EntityName} entity) => this.Return(({definition.EntityName})entity);");
		sb.AppendLine("    }");
		sb.AppendLine("}");
		return sb.ToString();
	}

	private static string GeneratePrefabPoolContent(EntityDomainDefinition definition, CodeGenConfig config, string fileName)
	{
		StringBuilder sb = new StringBuilder();
		sb.Append(EntityDomainFileHelper.GetFileHeader(definition, fileName, config));
		sb.AppendLine();
		sb.AppendLine("using Atomic.Entities;");
		string[] imports = definition.GetImports();
		foreach (string text in imports)
		{
			if (!string.IsNullOrWhiteSpace(text))
			{
				string text2 = text.Trim();
				sb.AppendLine(text2.StartsWith("using") ? text2 : ("using " + text2 + ";"));
			}
		}
		sb.AppendLine();
		sb.AppendLine($"namespace {definition.Namespace}");
		sb.AppendLine("{");
		sb.AppendLine("    /// <summary>");
		sb.AppendLine($"    /// A prefab-based entity pool for managing <see cref=\"{definition.EntityName}\"/> instances at runtime.");
		sb.AppendLine("    /// </summary>");
		sb.AppendLine("    /// <remarks>");
		sb.AppendLine($"    /// Useful for dynamically spawning and reusing <see cref=\"{definition.EntityName}\"/> prefabs in gameplay scenes.");
		sb.AppendLine("    /// </remarks>");
		sb.AppendLine($"    public sealed class {definition.EntityName}PrefabPool : PrefabEntityPool<{definition.EntityName}>");
		sb.AppendLine("    {");
		sb.AppendLine("    }");
		sb.AppendLine("}");
		return sb.ToString();
	}
}
