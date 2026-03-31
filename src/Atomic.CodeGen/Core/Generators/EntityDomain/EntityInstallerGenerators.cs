using System.IO;
using System.Text;
using System.Threading.Tasks;
using Atomic.CodeGen.Core.Models;
using Atomic.CodeGen.Core.Models.EntityDomain;
using Atomic.CodeGen.Utils;

namespace Atomic.CodeGen.Core.Generators.EntityDomain;

public static class EntityInstallerGenerators
{
	public static async Task GenerateScriptableInstallerAsync(EntityDomainDefinition definition, CodeGenConfig config, string outputDir)
	{
		bool flag = definition.Installers.HasFlag(EntityInstallerMode.ScriptableEntityInstaller) && definition.Installers.HasFlag(EntityInstallerMode.SceneEntityInstaller);
		string text = (flag ? "Scriptable" : "");
		string fileName = text + definition.EntityName + "Installer.cs";
		string filePath = Path.Combine(outputDir, fileName);
		string contents = GenerateInstallerContent(definition, config, fileName, isScriptable: true, flag);
		await File.WriteAllTextAsync(filePath, contents);
		await EntityDomainFileHelper.GenerateMetaFileAsync(filePath);
		await EntityDomainFileHelper.LinkToProjectsAsync(definition, config, filePath);
		Logger.LogVerbose("Generated: " + fileName);
	}

	public static async Task GenerateSceneInstallerAsync(EntityDomainDefinition definition, CodeGenConfig config, string outputDir)
	{
		bool flag = definition.Installers.HasFlag(EntityInstallerMode.ScriptableEntityInstaller) && definition.Installers.HasFlag(EntityInstallerMode.SceneEntityInstaller);
		string text = (flag ? "Scene" : "");
		string fileName = text + definition.EntityName + "Installer.cs";
		string filePath = Path.Combine(outputDir, fileName);
		string contents = GenerateInstallerContent(definition, config, fileName, isScriptable: false, flag);
		await File.WriteAllTextAsync(filePath, contents);
		await EntityDomainFileHelper.GenerateMetaFileAsync(filePath);
		await EntityDomainFileHelper.LinkToProjectsAsync(definition, config, filePath);
		Logger.LogVerbose("Generated: " + fileName);
	}

	public static async Task GenerateInstallerInterfaceAsync(EntityDomainDefinition definition, CodeGenConfig config, string outputDir)
	{
		string fileName = "I" + definition.EntityName + "Installer.cs";
		string filePath = Path.Combine(outputDir, fileName);
		string contents = GenerateInstallerInterfaceContent(definition, config, fileName);
		await File.WriteAllTextAsync(filePath, contents);
		await EntityDomainFileHelper.GenerateMetaFileAsync(filePath);
		await EntityDomainFileHelper.LinkToProjectsAsync(definition, config, filePath);
		Logger.LogVerbose("Generated: " + fileName);
	}

	private static string GenerateInstallerContent(EntityDomainDefinition definition, CodeGenConfig config, string fileName, bool isScriptable, bool usePrefixes)
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
			handler = new StringBuilder.AppendInterpolatedStringHandler(124, 1, stringBuilder2);
			handler.AppendLiteral("    /// A Unity <see cref=\"ScriptableObject\"/> that defines reusable logic for installing or configuring an <see cref=\"I");
			handler.AppendFormatted(definition.EntityName);
			handler.AppendLiteral("\"/>.");
			stringBuilder4.AppendLine(ref handler);
			stringBuilder.AppendLine("    /// </summary>");
			stringBuilder.AppendLine("    /// <remarks>");
			stringBuilder.AppendLine("    /// This is useful for defining shared configuration logic that can be applied to multiple entities,");
			stringBuilder.AppendLine("    /// such as setting default values, tags, or attaching behaviors.");
			stringBuilder.AppendLine("    /// Supports both runtime and edit-time contexts via utility methods.");
		}
		else
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder5 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(166, 1, stringBuilder2);
			handler.AppendLiteral("    /// A Unity <see cref=\"MonoBehaviour\"/> that can be attached to a GameObject to perform installation logic on an <see cref=\"I");
			handler.AppendFormatted(definition.EntityName);
			handler.AppendLiteral("\"/> during runtime or initialization.");
			stringBuilder5.AppendLine(ref handler);
			stringBuilder.AppendLine("    /// </summary>");
			stringBuilder.AppendLine("    /// <remarks>");
			stringBuilder.AppendLine("    /// Used to declaratively configure entities placed in a scene.");
			stringBuilder.AppendLine("    /// In the Editor, it supports automatic refresh via <c>OnValidate</c>.");
		}
		stringBuilder.AppendLine("    /// </remarks>");
		string value = ((!usePrefixes) ? "" : (isScriptable ? "Scriptable" : "Scene"));
		string value2 = (isScriptable ? "ScriptableEntityInstaller" : "SceneEntityInstaller");
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder6 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(41, 4, stringBuilder2);
		handler.AppendLiteral("    public abstract class ");
		handler.AppendFormatted(value);
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral("Installer : ");
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

	private static string GenerateInstallerInterfaceContent(EntityDomainDefinition definition, CodeGenConfig config, string fileName)
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
		handler = new StringBuilder.AppendInterpolatedStringHandler(85, 1, stringBuilder2);
		handler.AppendLiteral("    /// An interface contract for installers that configure <see cref=\"I");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral("\"/> entities.");
		stringBuilder4.AppendLine(ref handler);
		stringBuilder.AppendLine("    /// </summary>");
		stringBuilder.AppendLine("    /// <remarks>");
		stringBuilder.AppendLine("    /// Implement this interface to define custom installation logic that can be applied to entities.");
		stringBuilder.AppendLine("    /// This provides a common contract for both scriptable and scene-based installers.");
		stringBuilder.AppendLine("    /// </remarks>");
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder5 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(53, 2, stringBuilder2);
		handler.AppendLiteral("    public interface I");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral("Installer : IEntityInstaller<I");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral(">");
		stringBuilder5.AppendLine(ref handler);
		stringBuilder.AppendLine("    {");
		stringBuilder.AppendLine("    }");
		stringBuilder.AppendLine("}");
		return stringBuilder.ToString();
	}
}
