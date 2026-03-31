using System.IO;
using System.Text;
using System.Threading.Tasks;
using Atomic.CodeGen.Core.Models;
using Atomic.CodeGen.Core.Models.EntityDomain;
using Atomic.CodeGen.Utils;

namespace Atomic.CodeGen.Core.Generators.EntityDomain;

public static class EntityWorldGenerator
{
	public static async Task GenerateAsync(EntityDomainDefinition definition, CodeGenConfig config, string outputDir)
	{
		string fileName = definition.EntityName + "World.cs";
		string filePath = Path.Combine(outputDir, fileName);
		string contents = GenerateContent(definition, config, fileName);
		await File.WriteAllTextAsync(filePath, contents);
		await EntityDomainFileHelper.GenerateMetaFileAsync(filePath);
		await EntityDomainFileHelper.LinkToProjectsAsync(definition, config, filePath);
		Logger.LogVerbose("Generated: " + fileName);
	}

	private static string GenerateContent(EntityDomainDefinition definition, CodeGenConfig config, string fileName)
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
		handler = new StringBuilder.AppendInterpolatedStringHandler(76, 1, stringBuilder2);
		handler.AppendLiteral("    /// A world container for <see cref=\"I");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral("\"/> entities in the current scene.");
		stringBuilder4.AppendLine(ref handler);
		stringBuilder.AppendLine("    /// Manages the collection of scene-based entities and their lifecycle.");
		stringBuilder.AppendLine("    /// </summary>");
		stringBuilder.AppendLine("    /// <remarks>");
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder5 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(86, 1, stringBuilder2);
		handler.AppendLiteral("    /// Use this class to access all <see cref=\"");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral("\"/> instances within the active scene.");
		stringBuilder5.AppendLine(ref handler);
		stringBuilder.AppendLine("    /// It provides centralized management for entity creation, destruction, and queries.");
		stringBuilder.AppendLine("    /// </remarks>");
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder6 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(50, 2, stringBuilder2);
		handler.AppendLiteral("    public sealed class ");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral("World : SceneEntityWorld<");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral(">");
		stringBuilder6.AppendLine(ref handler);
		stringBuilder.AppendLine("    {");
		stringBuilder.AppendLine("    }");
		stringBuilder.AppendLine("}");
		return stringBuilder.ToString();
	}
}
