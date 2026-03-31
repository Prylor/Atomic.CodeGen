using System.IO;
using System.Text;
using System.Threading.Tasks;
using Atomic.CodeGen.Core.Models;
using Atomic.CodeGen.Core.Models.EntityDomain;
using Atomic.CodeGen.Utils;

namespace Atomic.CodeGen.Core.Generators.EntityDomain;

public static class EntityAspectGenerators
{
	public static async Task GenerateScriptableAspectAsync(EntityDomainDefinition definition, CodeGenConfig config, string outputDir)
	{
		bool flag = definition.Aspects.HasFlag(EntityAspectMode.ScriptableEntityAspect) && definition.Aspects.HasFlag(EntityAspectMode.SceneEntityAspect);
		string text = (flag ? "Scriptable" : "");
		string fileName = text + definition.EntityName + "Aspect.cs";
		string filePath = Path.Combine(outputDir, fileName);
		string contents = GenerateAspectContent(definition, config, fileName, isScriptable: true, flag);
		await File.WriteAllTextAsync(filePath, contents);
		await EntityDomainFileHelper.GenerateMetaFileAsync(filePath);
		await EntityDomainFileHelper.LinkToProjectsAsync(definition, config, filePath);
		Logger.LogVerbose("Generated: " + fileName);
	}

	public static async Task GenerateSceneAspectAsync(EntityDomainDefinition definition, CodeGenConfig config, string outputDir)
	{
		bool flag = definition.Aspects.HasFlag(EntityAspectMode.ScriptableEntityAspect) && definition.Aspects.HasFlag(EntityAspectMode.SceneEntityAspect);
		string text = (flag ? "Scene" : "");
		string fileName = text + definition.EntityName + "Aspect.cs";
		string filePath = Path.Combine(outputDir, fileName);
		string contents = GenerateAspectContent(definition, config, fileName, isScriptable: false, flag);
		await File.WriteAllTextAsync(filePath, contents);
		await EntityDomainFileHelper.GenerateMetaFileAsync(filePath);
		await EntityDomainFileHelper.LinkToProjectsAsync(definition, config, filePath);
		Logger.LogVerbose("Generated: " + fileName);
	}

	private static string GenerateAspectContent(EntityDomainDefinition definition, CodeGenConfig config, string fileName, bool isScriptable, bool usePrefixes)
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
		if (isScriptable)
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder4 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(97, 1, stringBuilder2);
			handler.AppendLiteral("    /// A Unity <see cref=\"ScriptableObject\"/> that applies reusable logic to an <see cref=\"I");
			handler.AppendFormatted(definition.EntityName);
			handler.AppendLiteral("\"/>.");
			stringBuilder4.AppendLine(ref handler);
			stringBuilder.AppendLine("    /// </summary>");
			stringBuilder.AppendLine("    /// <remarks>");
			stringBuilder.AppendLine("    /// Aspects define shared, composable behaviors that can be applied to entities.");
			stringBuilder.AppendLine("    /// Use this for cross-cutting concerns like logging, analytics, or state validation.");
			stringBuilder.AppendLine("    /// Supports both runtime and edit-time contexts via utility methods.");
		}
		else
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder5 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(111, 1, stringBuilder2);
			handler.AppendLiteral("    /// A Unity <see cref=\"MonoBehaviour\"/> that applies scene-specific logic to an <see cref=\"I");
			handler.AppendFormatted(definition.EntityName);
			handler.AppendLiteral("\"/> at runtime.");
			stringBuilder5.AppendLine(ref handler);
			stringBuilder.AppendLine("    /// </summary>");
			stringBuilder.AppendLine("    /// <remarks>");
			stringBuilder.AppendLine("    /// Used for applying scene-based aspects to entities placed in the scene hierarchy.");
			stringBuilder.AppendLine("    /// In the Editor, it supports automatic refresh via <c>OnValidate</c>.");
		}
		stringBuilder.AppendLine("    /// </remarks>");
		string value = ((!usePrefixes) ? "" : (isScriptable ? "Scriptable" : "Scene"));
		string value2 = (isScriptable ? "ScriptableEntityAspect" : "SceneEntityAspect");
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder6 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(38, 4, stringBuilder2);
		handler.AppendLiteral("    public abstract class ");
		handler.AppendFormatted(value);
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral("Aspect : ");
		handler.AppendFormatted(value2);
		handler.AppendLiteral("<I");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral(">");
		stringBuilder6.AppendLine(ref handler);
		stringBuilder.AppendLine("    {");
		stringBuilder.AppendLine("    }");
		stringBuilder.AppendLine("}");
		return stringBuilder.ToString();
	}
}
