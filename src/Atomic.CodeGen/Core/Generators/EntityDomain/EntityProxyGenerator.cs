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
		handler = new StringBuilder.AppendInterpolatedStringHandler(129, 2, stringBuilder2);
		handler.AppendLiteral("    /// A Unity <see cref=\"MonoBehaviour\"/> proxy that forwards all <see cref=\"I");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral("\"/> calls to an underlying <see cref=\"");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral("\"/> entity.");
		stringBuilder4.AppendLine(ref handler);
		stringBuilder.AppendLine("    /// </summary>");
		stringBuilder.AppendLine("    /// <remarks>");
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder5 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(134, 1, stringBuilder2);
		handler.AppendLiteral("    /// This proxy allows interacting with an <see cref=\"I");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral("\"/> instance inside the Unity scene while decoupling logic from GameObjects.");
		stringBuilder5.AppendLine(ref handler);
		stringBuilder.AppendLine("    /// It acts as a transparent forwarder for all entity functionality — values, lifecycle, tags, and behaviours.");
		stringBuilder.AppendLine("    ///");
		stringBuilder.AppendLine("    /// Use this component to expose scene-level access to an entity while keeping logic modular and testable.");
		stringBuilder.AppendLine("    ///");
		stringBuilder.AppendLine("    /// **Collider Interaction Note**:");
		stringBuilder.AppendLine("    /// If your entity includes multiple colliders (e.g., hitboxes or triggers),");
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder6 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(80, 2, stringBuilder2);
		handler.AppendLiteral("    /// place <c>");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral("Proxy</c> on each and reference the same source <see cref=\"");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral("\"/>.");
		stringBuilder6.AppendLine(ref handler);
		stringBuilder.AppendLine("    /// This provides unified access regardless of which collider was hit.");
		stringBuilder.AppendLine("    /// </remarks>");
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder7 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(53, 3, stringBuilder2);
		handler.AppendLiteral("    public sealed class ");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral("Proxy : SceneEntityProxy<");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral(">, I");
		handler.AppendFormatted(definition.EntityName);
		stringBuilder7.AppendLine(ref handler);
		stringBuilder.AppendLine("    {");
		stringBuilder.AppendLine("    }");
		stringBuilder.AppendLine("}");
		return stringBuilder.ToString();
	}
}
