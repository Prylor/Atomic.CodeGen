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
		sb.AppendLine($"    /// A world container for <see cref=\"I{definition.EntityName}\"/> entities in the current scene.");
		sb.AppendLine("    /// Manages the collection of scene-based entities and their lifecycle.");
		sb.AppendLine("    /// </summary>");
		sb.AppendLine("    /// <remarks>");
		sb.AppendLine($"    /// Use this class to access all <see cref=\"{definition.EntityName}\"/> instances within the active scene.");
		sb.AppendLine("    /// It provides centralized management for entity creation, destruction, and queries.");
		sb.AppendLine("    /// </remarks>");
		sb.AppendLine($"    public sealed class {definition.EntityName}World : SceneEntityWorld<{definition.EntityName}>");
		sb.AppendLine("    {");
		sb.AppendLine("    }");
		sb.AppendLine("}");
		return sb.ToString();
	}
}
