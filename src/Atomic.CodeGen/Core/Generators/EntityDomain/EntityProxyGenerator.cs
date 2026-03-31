using System.IO;
using System.Text;
using System.Threading.Tasks;
using Atomic.CodeGen.Core.Models;
using Atomic.CodeGen.Core.Models.EntityDomain;
using Atomic.CodeGen.Utils;

namespace Atomic.CodeGen.Core.Generators.EntityDomain;

public static class EntityProxyGenerator
{
	public static async Task GenerateAsync(EntityDomainDefinition definition, CodeGenConfig config, string outputDir)
	{
		string fileName = definition.EntityName + "Proxy.cs";
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
		sb.AppendLine($"    /// A Unity <see cref=\"MonoBehaviour\"/> proxy that forwards all <see cref=\"I{definition.EntityName}\"/> calls to an underlying <see cref=\"{definition.EntityName}\"/> entity.");
		sb.AppendLine("    /// </summary>");
		sb.AppendLine("    /// <remarks>");
		sb.AppendLine($"    /// This proxy allows interacting with an <see cref=\"I{definition.EntityName}\"/> instance inside the Unity scene while decoupling logic from GameObjects.");
		sb.AppendLine("    /// It acts as a transparent forwarder for all entity functionality — values, lifecycle, tags, and behaviours.");
		sb.AppendLine("    ///");
		sb.AppendLine("    /// Use this component to expose scene-level access to an entity while keeping logic modular and testable.");
		sb.AppendLine("    ///");
		sb.AppendLine("    /// **Collider Interaction Note**:");
		sb.AppendLine("    /// If your entity includes multiple colliders (e.g., hitboxes or triggers),");
		sb.AppendLine($"    /// place <c>{definition.EntityName}Proxy</c> on each and reference the same source <see cref=\"{definition.EntityName}\"/>.");
		sb.AppendLine("    /// This provides unified access regardless of which collider was hit.");
		sb.AppendLine("    /// </remarks>");
		sb.AppendLine($"    public sealed class {definition.EntityName}Proxy : SceneEntityProxy<{definition.EntityName}>, I{definition.EntityName}");
		sb.AppendLine("    {");
		sb.AppendLine("    }");
		sb.AppendLine("}");
		return sb.ToString();
	}
}
