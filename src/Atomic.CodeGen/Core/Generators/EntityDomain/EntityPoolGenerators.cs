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
		handler = new StringBuilder.AppendInterpolatedStringHandler(86, 1, stringBuilder2);
		handler.AppendLiteral("    /// A Unity-integrated pool for <see cref=\"");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral("\"/> entities that exist within a scene.");
		stringBuilder4.AppendLine(ref handler);
		stringBuilder.AppendLine("    /// </summary>");
		stringBuilder.AppendLine("    /// <remarks>");
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder5 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(95, 1, stringBuilder2);
		handler.AppendLiteral("    /// Implements <see cref=\"IEntityPool{I");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral("}\"/> for renting and returning scene-based entities.");
		stringBuilder5.AppendLine(ref handler);
		stringBuilder.AppendLine("    /// </remarks>");
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder6 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(64, 3, stringBuilder2);
		handler.AppendLiteral("    public sealed class ");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral("Pool : SceneEntityPool<");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral(">, IEntityPool<I");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral(">");
		stringBuilder6.AppendLine(ref handler);
		stringBuilder.AppendLine("    {");
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder7 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(47, 2, stringBuilder2);
		handler.AppendLiteral("        I");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral(" IEntityPool<I");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral(">.Rent() => this.Rent();");
		stringBuilder7.AppendLine(ref handler);
		stringBuilder.AppendLine();
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder8 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(70, 3, stringBuilder2);
		handler.AppendLiteral("        void IEntityPool<I");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral(">.Return(I");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral(" entity) => this.Return((");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral(")entity);");
		stringBuilder8.AppendLine(ref handler);
		stringBuilder.AppendLine("    }");
		stringBuilder.AppendLine("}");
		return stringBuilder.ToString();
	}

	private static string GeneratePrefabPoolContent(EntityDomainDefinition definition, CodeGenConfig config, string fileName)
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
		handler = new StringBuilder.AppendInterpolatedStringHandler(84, 1, stringBuilder2);
		handler.AppendLiteral("    /// A prefab-based entity pool for managing <see cref=\"");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral("\"/> instances at runtime.");
		stringBuilder4.AppendLine(ref handler);
		stringBuilder.AppendLine("    /// </summary>");
		stringBuilder.AppendLine("    /// <remarks>");
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder5 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(94, 1, stringBuilder2);
		handler.AppendLiteral("    /// Useful for dynamically spawning and reusing <see cref=\"");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral("\"/> prefabs in gameplay scenes.");
		stringBuilder5.AppendLine(ref handler);
		stringBuilder.AppendLine("    /// </remarks>");
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder6 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(55, 2, stringBuilder2);
		handler.AppendLiteral("    public sealed class ");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral("PrefabPool : PrefabEntityPool<");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral(">");
		stringBuilder6.AppendLine(ref handler);
		stringBuilder.AppendLine("    {");
		stringBuilder.AppendLine("    }");
		stringBuilder.AppendLine("}");
		return stringBuilder.ToString();
	}
}
