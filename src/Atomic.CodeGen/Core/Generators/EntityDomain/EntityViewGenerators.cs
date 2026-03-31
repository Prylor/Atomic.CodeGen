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
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append(EntityDomainFileHelper.GetFileHeader(definition, fileName, config));
		stringBuilder.AppendLine();
		stringBuilder.AppendLine("using Atomic.Entities;");
		stringBuilder.AppendLine("using UnityEngine;");
		string[] imports = definition.GetImports();
		foreach (string text in imports)
		{
			if (!string.IsNullOrWhiteSpace(text))
			{
				string text2 = text.Trim();
				stringBuilder.AppendLine(text2.StartsWith("using") ? text2 : ("using " + text2 + ";"));
			}
		}
		stringBuilder.AppendLine();
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder3 = stringBuilder2;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(10, 1, stringBuilder2);
		handler.AppendLiteral("namespace ");
		handler.AppendFormatted(definition.Namespace);
		stringBuilder3.AppendLine(ref handler);
		stringBuilder.AppendLine("{");
		stringBuilder.AppendLine("    /// <summary>");
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder4 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(94, 1, stringBuilder2);
		handler.AppendLiteral("    /// A Unity <see cref=\"MonoBehaviour\"/> view component bound to an <see cref=\"I");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral("\"/> entity.");
		stringBuilder4.AppendLine(ref handler);
		stringBuilder.AppendLine("    /// </summary>");
		stringBuilder.AppendLine("    /// <remarks>");
		stringBuilder.AppendLine("    /// This component provides visual representation and Unity-specific functionality for the entity.");
		stringBuilder.AppendLine("    /// </remarks>");
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder5 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(37, 2, stringBuilder2);
		handler.AppendLiteral("    public class ");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral("View : EntityView<I");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral(">");
		stringBuilder5.AppendLine(ref handler);
		stringBuilder.AppendLine("    {");
		stringBuilder.AppendLine("    }");
		stringBuilder.AppendLine("}");
		return stringBuilder.ToString();
	}

	private static string GenerateEntityViewCatalogContent(EntityDomainDefinition definition, CodeGenConfig config, string fileName)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append(EntityDomainFileHelper.GetFileHeader(definition, fileName, config));
		stringBuilder.AppendLine();
		stringBuilder.AppendLine("using Atomic.Entities;");
		stringBuilder.AppendLine("using UnityEngine;");
		string[] imports = definition.GetImports();
		foreach (string text in imports)
		{
			if (!string.IsNullOrWhiteSpace(text))
			{
				string text2 = text.Trim();
				stringBuilder.AppendLine(text2.StartsWith("using") ? text2 : ("using " + text2 + ";"));
			}
		}
		stringBuilder.AppendLine();
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder3 = stringBuilder2;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(10, 1, stringBuilder2);
		handler.AppendLiteral("namespace ");
		handler.AppendFormatted(definition.Namespace);
		stringBuilder3.AppendLine(ref handler);
		stringBuilder.AppendLine("{");
		stringBuilder.AppendLine("    /// <summary>");
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder4 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(108, 1, stringBuilder2);
		handler.AppendLiteral("    /// A <see cref=\"ScriptableObject\"/> catalog that maps entity identifiers to <see cref=\"");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral("View\"/> prefabs.");
		stringBuilder4.AppendLine(ref handler);
		stringBuilder.AppendLine("    /// </summary>");
		stringBuilder.AppendLine("    /// <remarks>");
		stringBuilder.AppendLine("    /// Use this catalog to define reusable view prefab mappings for different entity configurations.");
		stringBuilder.AppendLine("    /// </remarks>");
		string value = definition.Namespace.Replace('.', '/');
		stringBuilder.AppendLine("    [CreateAssetMenu(");
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder5 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(33, 1, stringBuilder2);
		handler.AppendLiteral("        fileName = \"");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral("ViewCatalog\",");
		stringBuilder5.AppendLine(ref handler);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder6 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(37, 2, stringBuilder2);
		handler.AppendLiteral("        menuName = \"");
		handler.AppendFormatted(value);
		handler.AppendLiteral("/New ");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral("ViewCatalog\"");
		stringBuilder6.AppendLine(ref handler);
		stringBuilder.AppendLine("    )]");
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder7 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(64, 3, stringBuilder2);
		handler.AppendLiteral("    public sealed class ");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral("ViewCatalog : EntityViewCatalog<I");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral(", ");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral("View>");
		stringBuilder7.AppendLine(ref handler);
		stringBuilder.AppendLine("    {");
		stringBuilder.AppendLine("    }");
		stringBuilder.AppendLine("}");
		return stringBuilder.ToString();
	}

	private static string GenerateEntityViewPoolContent(EntityDomainDefinition definition, CodeGenConfig config, string fileName)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append(EntityDomainFileHelper.GetFileHeader(definition, fileName, config));
		stringBuilder.AppendLine();
		stringBuilder.AppendLine("using Atomic.Entities;");
		string[] imports = definition.GetImports();
		foreach (string text in imports)
		{
			if (!string.IsNullOrWhiteSpace(text))
			{
				string text2 = text.Trim();
				stringBuilder.AppendLine(text2.StartsWith("using") ? text2 : ("using " + text2 + ";"));
			}
		}
		stringBuilder.AppendLine();
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder3 = stringBuilder2;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(10, 1, stringBuilder2);
		handler.AppendLiteral("namespace ");
		handler.AppendFormatted(definition.Namespace);
		stringBuilder3.AppendLine(ref handler);
		stringBuilder.AppendLine("{");
		stringBuilder.AppendLine("    /// <summary>");
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder4 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(57, 1, stringBuilder2);
		handler.AppendLiteral("    /// A pool for managing <see cref=\"");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral("View\"/> instances.");
		stringBuilder4.AppendLine(ref handler);
		stringBuilder.AppendLine("    /// </summary>");
		stringBuilder.AppendLine("    /// <remarks>");
		stringBuilder.AppendLine("    /// Use this pool to efficiently reuse view instances instead of constantly creating and destroying them.");
		stringBuilder.AppendLine("    /// </remarks>");
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder5 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(58, 3, stringBuilder2);
		handler.AppendLiteral("    public sealed class ");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral("ViewPool : EntityViewPool<I");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral(", ");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral("View>");
		stringBuilder5.AppendLine(ref handler);
		stringBuilder.AppendLine("    {");
		stringBuilder.AppendLine("    }");
		stringBuilder.AppendLine("}");
		return stringBuilder.ToString();
	}

	private static string GenerateEntityCollectionViewContent(EntityDomainDefinition definition, CodeGenConfig config, string fileName)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append(EntityDomainFileHelper.GetFileHeader(definition, fileName, config));
		stringBuilder.AppendLine();
		stringBuilder.AppendLine("using Atomic.Entities;");
		string[] imports = definition.GetImports();
		foreach (string text in imports)
		{
			if (!string.IsNullOrWhiteSpace(text))
			{
				string text2 = text.Trim();
				stringBuilder.AppendLine(text2.StartsWith("using") ? text2 : ("using " + text2 + ";"));
			}
		}
		stringBuilder.AppendLine();
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder3 = stringBuilder2;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(10, 1, stringBuilder2);
		handler.AppendLiteral("namespace ");
		handler.AppendFormatted(definition.Namespace);
		stringBuilder3.AppendLine(ref handler);
		stringBuilder.AppendLine("{");
		stringBuilder.AppendLine("    /// <summary>");
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder4 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(85, 1, stringBuilder2);
		handler.AppendLiteral("    /// A collection view manager for handling multiple <see cref=\"");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral("View\"/> instances.");
		stringBuilder4.AppendLine(ref handler);
		stringBuilder.AppendLine("    /// </summary>");
		stringBuilder.AppendLine("    /// <remarks>");
		stringBuilder.AppendLine("    /// This component manages synchronization between a collection of entities and their corresponding views.");
		stringBuilder.AppendLine("    /// </remarks>");
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder5 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(70, 3, stringBuilder2);
		handler.AppendLiteral("    public sealed class ");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral("CollectionView : EntityCollectionView<I");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral(", ");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral("View>");
		stringBuilder5.AppendLine(ref handler);
		stringBuilder.AppendLine("    {");
		stringBuilder.AppendLine("    }");
		stringBuilder.AppendLine("}");
		return stringBuilder.ToString();
	}
}
