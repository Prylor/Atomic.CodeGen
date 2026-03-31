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
		if (isScriptable)
		{
			sb.AppendLine($"    /// A Unity <see cref=\"ScriptableObject\"/> that applies reusable logic to an <see cref=\"I{definition.EntityName}\"/>.");
			sb.AppendLine("    /// </summary>");
			sb.AppendLine("    /// <remarks>");
			sb.AppendLine("    /// Aspects define shared, composable behaviors that can be applied to entities.");
			sb.AppendLine("    /// Use this for cross-cutting concerns like logging, analytics, or state validation.");
			sb.AppendLine("    /// Supports both runtime and edit-time contexts via utility methods.");
		}
		else
		{
			sb.AppendLine($"    /// A Unity <see cref=\"MonoBehaviour\"/> that applies scene-specific logic to an <see cref=\"I{definition.EntityName}\"/> at runtime.");
			sb.AppendLine("    /// </summary>");
			sb.AppendLine("    /// <remarks>");
			sb.AppendLine("    /// Used for applying scene-based aspects to entities placed in the scene hierarchy.");
			sb.AppendLine("    /// In the Editor, it supports automatic refresh via <c>OnValidate</c>.");
		}
		sb.AppendLine("    /// </remarks>");
		string value = ((!usePrefixes) ? "" : (isScriptable ? "Scriptable" : "Scene"));
		string value2 = (isScriptable ? "ScriptableEntityAspect" : "SceneEntityAspect");
		sb.AppendLine($"    public abstract class {value}{definition.EntityName}Aspect : {value2}<I{definition.EntityName}>");
		sb.AppendLine("    {");
		sb.AppendLine("    }");
		sb.AppendLine("}");
		return sb.ToString();
	}
}
