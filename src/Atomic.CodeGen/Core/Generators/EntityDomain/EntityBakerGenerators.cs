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
		if (isOptimized)
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder4 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(90, 1, stringBuilder2);
			handler.AppendLiteral("    /// An optimized baker for converting scene GameObjects into <see cref=\"I");
			handler.AppendFormatted(definition.EntityName);
			handler.AppendLiteral("\"/> entities.");
			stringBuilder4.AppendLine(ref handler);
			stringBuilder.AppendLine("    /// </summary>");
			stringBuilder.AppendLine("    /// <remarks>");
			stringBuilder.AppendLine("    /// This optimized version caches the view component for better performance during batch conversions.");
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder5 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(71, 1, stringBuilder2);
			handler.AppendLiteral("    /// Requires a <see cref=\"");
			handler.AppendFormatted(definition.EntityName);
			handler.AppendLiteral("View\"/> component on the same GameObject.");
			stringBuilder5.AppendLine(ref handler);
		}
		else
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder6 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(79, 1, stringBuilder2);
			handler.AppendLiteral("    /// A baker for converting scene GameObjects into <see cref=\"I");
			handler.AppendFormatted(definition.EntityName);
			handler.AppendLiteral("\"/> entities.");
			stringBuilder6.AppendLine(ref handler);
			stringBuilder.AppendLine("    /// </summary>");
			stringBuilder.AppendLine("    /// <remarks>");
			stringBuilder.AppendLine("    /// Override this class to define custom conversion logic from authoring components to runtime entities.");
		}
		stringBuilder.AppendLine("    /// </remarks>");
		if (isOptimized)
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder7 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(36, 1, stringBuilder2);
			handler.AppendLiteral("    [RequireComponent(typeof(");
			handler.AppendFormatted(definition.EntityName);
			handler.AppendLiteral("View))]");
			stringBuilder7.AppendLine(ref handler);
		}
		string text3 = ((useSuffixes && isOptimized) ? "Optimized" : "");
		string value = definition.EntityName + "Baker" + text3;
		string value2 = (isOptimized ? $"SceneEntityBakerOptimized<I{definition.EntityName}, {definition.EntityName}View>" : ("SceneEntityBaker<I" + definition.EntityName + ">"));
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder8 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(29, 2, stringBuilder2);
		handler.AppendLiteral("    public abstract class ");
		handler.AppendFormatted(value);
		handler.AppendLiteral(" : ");
		handler.AppendFormatted(value2);
		stringBuilder8.AppendLine(ref handler);
		stringBuilder.AppendLine("    {");
		stringBuilder.AppendLine("    }");
		stringBuilder.AppendLine("}");
		return stringBuilder.ToString();
	}
}
