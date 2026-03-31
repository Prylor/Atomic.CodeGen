using System.IO;
using System.Text;
using System.Threading.Tasks;
using Atomic.CodeGen.Core.Models;
using Atomic.CodeGen.Core.Models.EntityDomain;
using Atomic.CodeGen.Utils;

namespace Atomic.CodeGen.Core.Generators.EntityDomain;

public static class EntityInterfaceGenerator
{
	public static async Task GenerateAsync(EntityDomainDefinition definition, CodeGenConfig config, string outputDir)
	{
		string fileName = "I" + definition.EntityName + ".cs";
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
		stringBuilder.AppendLine("    /// Represents a specialized entity interface that extends the core <see cref=\"IEntity\"/> contract.");
		stringBuilder.AppendLine("    /// It follows the Entity–State–Behaviour architectural pattern, providing structure for identity (tags),");
		stringBuilder.AppendLine("    /// data (values), and modular behaviours within the Atomic Entity framework.");
		stringBuilder.AppendLine("    /// </summary>");
		stringBuilder.AppendLine("    /// <remarks>");
		stringBuilder.AppendLine("    /// Created by <b>Entity Domain Generator</b>.");
		stringBuilder.AppendLine("    /// </remarks>");
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder4 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(32, 1, stringBuilder2);
		handler.AppendLiteral("    public interface I");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral(" : IEntity");
		stringBuilder4.AppendLine(ref handler);
		stringBuilder.AppendLine("    {");
		stringBuilder.AppendLine("    }");
		stringBuilder.AppendLine("}");
		return stringBuilder.ToString();
	}
}
