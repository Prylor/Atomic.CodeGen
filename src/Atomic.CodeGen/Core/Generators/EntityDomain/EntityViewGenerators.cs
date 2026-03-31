using System.IO;
using System.Text;
using System.Threading.Tasks;
using Atomic.CodeGen.Core.Models;
using Atomic.CodeGen.Core.Models.EntityDomain;
using Atomic.CodeGen.Utils;

namespace Atomic.CodeGen.Core.Generators.EntityDomain;

public static class EntityViewGenerators
{
	public static async Task GenerateEntityViewAsync(EntityDomainDefinition definition, CodeGenConfig config, string outputDir)
	{
		string fileName = definition.EntityName + "View.cs";
		string filePath = Path.Combine(outputDir, fileName);
		string contents = GenerateEntityViewContent(definition, config, fileName);
		await File.WriteAllTextAsync(filePath, contents);
		await EntityDomainFileHelper.GenerateMetaFileAsync(filePath);
		await EntityDomainFileHelper.LinkToProjectsAsync(definition, config, filePath);
		Logger.LogVerbose("Generated: " + fileName);
	}

	public static async Task GenerateEntityViewCatalogAsync(EntityDomainDefinition definition, CodeGenConfig config, string outputDir)
	{
		string fileName = definition.EntityName + "ViewCatalog.cs";
		string filePath = Path.Combine(outputDir, fileName);
		string contents = GenerateEntityViewCatalogContent(definition, config, fileName);
		await File.WriteAllTextAsync(filePath, contents);
		await EntityDomainFileHelper.GenerateMetaFileAsync(filePath);
		await EntityDomainFileHelper.LinkToProjectsAsync(definition, config, filePath);
		Logger.LogVerbose("Generated: " + fileName);
	}

	public static async Task GenerateEntityViewPoolAsync(EntityDomainDefinition definition, CodeGenConfig config, string outputDir)
	{
		string fileName = definition.EntityName + "ViewPool.cs";
		string filePath = Path.Combine(outputDir, fileName);
		string contents = GenerateEntityViewPoolContent(definition, config, fileName);
		await File.WriteAllTextAsync(filePath, contents);
		await EntityDomainFileHelper.GenerateMetaFileAsync(filePath);
		await EntityDomainFileHelper.LinkToProjectsAsync(definition, config, filePath);
		Logger.LogVerbose("Generated: " + fileName);
	}

	public static async Task GenerateEntityCollectionViewAsync(EntityDomainDefinition definition, CodeGenConfig config, string outputDir)
	{
		string fileName = definition.EntityName + "CollectionView.cs";
		string filePath = Path.Combine(outputDir, fileName);
		string contents = GenerateEntityCollectionViewContent(definition, config, fileName);
		await File.WriteAllTextAsync(filePath, contents);
		await EntityDomainFileHelper.GenerateMetaFileAsync(filePath);
		await EntityDomainFileHelper.LinkToProjectsAsync(definition, config, filePath);
		Logger.LogVerbose("Generated: " + fileName);
	}

	private static string GenerateEntityViewContent(EntityDomainDefinition definition, CodeGenConfig config, string fileName)
	{
		StringBuilder sb = new StringBuilder();
		sb.Append(EntityDomainFileHelper.GetFileHeader(definition, fileName, config));
		sb.AppendLine();
		sb.AppendLine("using Atomic.Entities;");
		sb.AppendLine("using UnityEngine;");
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
		sb.AppendLine($"    /// A Unity <see cref=\"MonoBehaviour\"/> view component bound to an <see cref=\"I{definition.EntityName}\"/> entity.");
		sb.AppendLine("    /// </summary>");
		sb.AppendLine("    /// <remarks>");
		sb.AppendLine("    /// This component provides visual representation and Unity-specific functionality for the entity.");
		sb.AppendLine("    /// </remarks>");
		sb.AppendLine($"    public class {definition.EntityName}View : EntityView<I{definition.EntityName}>");
		sb.AppendLine("    {");
		sb.AppendLine("    }");
		sb.AppendLine("}");
		return sb.ToString();
	}

	private static string GenerateEntityViewCatalogContent(EntityDomainDefinition definition, CodeGenConfig config, string fileName)
	{
		StringBuilder sb = new StringBuilder();
		sb.Append(EntityDomainFileHelper.GetFileHeader(definition, fileName, config));
		sb.AppendLine();
		sb.AppendLine("using Atomic.Entities;");
		sb.AppendLine("using UnityEngine;");
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
		sb.AppendLine($"    /// A <see cref=\"ScriptableObject\"/> catalog that maps entity identifiers to <see cref=\"{definition.EntityName}View\"/> prefabs.");
		sb.AppendLine("    /// </summary>");
		sb.AppendLine("    /// <remarks>");
		sb.AppendLine("    /// Use this catalog to define reusable view prefab mappings for different entity configurations.");
		sb.AppendLine("    /// </remarks>");
		string value = definition.Namespace.Replace('.', '/');
		sb.AppendLine("    [CreateAssetMenu(");
		sb.AppendLine($"        fileName = \"{definition.EntityName}ViewCatalog\",");
		sb.AppendLine($"        menuName = \"{value}/New {definition.EntityName}ViewCatalog\"");
		sb.AppendLine("    )]");
		sb.AppendLine($"    public sealed class {definition.EntityName}ViewCatalog : EntityViewCatalog<I{definition.EntityName}, {definition.EntityName}View>");
		sb.AppendLine("    {");
		sb.AppendLine("    }");
		sb.AppendLine("}");
		return sb.ToString();
	}

	private static string GenerateEntityViewPoolContent(EntityDomainDefinition definition, CodeGenConfig config, string fileName)
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
		sb.AppendLine($"    /// A pool for managing <see cref=\"{definition.EntityName}View\"/> instances.");
		sb.AppendLine("    /// </summary>");
		sb.AppendLine("    /// <remarks>");
		sb.AppendLine("    /// Use this pool to efficiently reuse view instances instead of constantly creating and destroying them.");
		sb.AppendLine("    /// </remarks>");
		sb.AppendLine($"    public sealed class {definition.EntityName}ViewPool : EntityViewPool<I{definition.EntityName}, {definition.EntityName}View>");
		sb.AppendLine("    {");
		sb.AppendLine("    }");
		sb.AppendLine("}");
		return sb.ToString();
	}

	private static string GenerateEntityCollectionViewContent(EntityDomainDefinition definition, CodeGenConfig config, string fileName)
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
		sb.AppendLine($"    /// A collection view manager for handling multiple <see cref=\"{definition.EntityName}View\"/> instances.");
		sb.AppendLine("    /// </summary>");
		sb.AppendLine("    /// <remarks>");
		sb.AppendLine("    /// This component manages synchronization between a collection of entities and their corresponding views.");
		sb.AppendLine("    /// </remarks>");
		sb.AppendLine($"    public sealed class {definition.EntityName}CollectionView : EntityCollectionView<I{definition.EntityName}, {definition.EntityName}View>");
		sb.AppendLine("    {");
		sb.AppendLine("    }");
		sb.AppendLine("}");
		return sb.ToString();
	}
}
