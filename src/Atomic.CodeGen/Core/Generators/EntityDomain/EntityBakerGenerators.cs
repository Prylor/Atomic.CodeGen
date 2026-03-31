using System.IO;
using System.Text;
using System.Threading.Tasks;
using Atomic.CodeGen.Core.Models;
using Atomic.CodeGen.Core.Models.EntityDomain;
using Atomic.CodeGen.Utils;

namespace Atomic.CodeGen.Core.Generators.EntityDomain;

public static class EntityBakerGenerators
{
	public static async Task GenerateStandardBakerAsync(EntityDomainDefinition definition, CodeGenConfig config, string outputDir)
	{
		bool useSuffixes = definition.Bakers.HasFlag(EntityBakerMode.Standard) && definition.Bakers.HasFlag(EntityBakerMode.Optimized);
		string fileName = definition.EntityName + "Baker.cs";
		string filePath = Path.Combine(outputDir, fileName);
		string contents = GenerateBakerContent(definition, config, fileName, isOptimized: false, useSuffixes);
		await File.WriteAllTextAsync(filePath, contents);
		await EntityDomainFileHelper.GenerateMetaFileAsync(filePath);
		await EntityDomainFileHelper.LinkToProjectsAsync(definition, config, filePath);
		Logger.LogVerbose("Generated: " + fileName);
	}

	public static async Task GenerateOptimizedBakerAsync(EntityDomainDefinition definition, CodeGenConfig config, string outputDir)
	{
		bool flag = definition.Bakers.HasFlag(EntityBakerMode.Standard) && definition.Bakers.HasFlag(EntityBakerMode.Optimized);
		string text = (flag ? "Optimized" : "");
		string fileName = definition.EntityName + "Baker" + text + ".cs";
		string filePath = Path.Combine(outputDir, fileName);
		string contents = GenerateBakerContent(definition, config, fileName, isOptimized: true, flag);
		await File.WriteAllTextAsync(filePath, contents);
		await EntityDomainFileHelper.GenerateMetaFileAsync(filePath);
		await EntityDomainFileHelper.LinkToProjectsAsync(definition, config, filePath);
		Logger.LogVerbose("Generated: " + fileName);
	}

	private static string GenerateBakerContent(EntityDomainDefinition definition, CodeGenConfig config, string fileName, bool isOptimized, bool useSuffixes)
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
		if (isOptimized)
		{
			sb.AppendLine($"    /// An optimized baker for converting scene GameObjects into <see cref=\"I{definition.EntityName}\"/> entities.");
			sb.AppendLine("    /// </summary>");
			sb.AppendLine("    /// <remarks>");
			sb.AppendLine("    /// This optimized version caches the view component for better performance during batch conversions.");
			sb.AppendLine($"    /// Requires a <see cref=\"{definition.EntityName}View\"/> component on the same GameObject.");
		}
		else
		{
			sb.AppendLine($"    /// A baker for converting scene GameObjects into <see cref=\"I{definition.EntityName}\"/> entities.");
			sb.AppendLine("    /// </summary>");
			sb.AppendLine("    /// <remarks>");
			sb.AppendLine("    /// Override this class to define custom conversion logic from authoring components to runtime entities.");
		}
		sb.AppendLine("    /// </remarks>");
		if (isOptimized)
		{
			sb.AppendLine($"    [RequireComponent(typeof({definition.EntityName}View))]");
		}
		string text3 = ((useSuffixes && isOptimized) ? "Optimized" : "");
		string value = definition.EntityName + "Baker" + text3;
		string value2 = (isOptimized ? $"SceneEntityBakerOptimized<I{definition.EntityName}, {definition.EntityName}View>" : ("SceneEntityBaker<I" + definition.EntityName + ">"));
		sb.AppendLine($"    public abstract class {value} : {value2}");
		sb.AppendLine("    {");
		sb.AppendLine("    }");
		sb.AppendLine("}");
		return sb.ToString();
	}
}
