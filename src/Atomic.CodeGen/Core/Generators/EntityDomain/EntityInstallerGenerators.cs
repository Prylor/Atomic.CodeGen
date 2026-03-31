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
		bool hasBothInstallers = definition.Installers.HasFlag(EntityInstallerMode.ScriptableEntityInstaller) && definition.Installers.HasFlag(EntityInstallerMode.SceneEntityInstaller);
		string prefix = (hasBothInstallers ? "Scriptable" : "");
		string fileName = prefix + definition.EntityName + "Installer.cs";
		string filePath = Path.Combine(outputDir, fileName);
		string contents = GenerateInstallerContent(definition, config, fileName, isScriptable: true, hasBothInstallers);
		await File.WriteAllTextAsync(filePath, contents);
		await EntityDomainFileHelper.GenerateMetaFileAsync(filePath);
		await EntityDomainFileHelper.LinkToProjectsAsync(definition, config, filePath);
		Logger.LogVerbose("Generated: " + fileName);
	}

	public static async Task GenerateSceneInstallerAsync(EntityDomainDefinition definition, CodeGenConfig config, string outputDir)
	{
		bool hasBothInstallers = definition.Installers.HasFlag(EntityInstallerMode.ScriptableEntityInstaller) && definition.Installers.HasFlag(EntityInstallerMode.SceneEntityInstaller);
		string prefix = (hasBothInstallers ? "Scene" : "");
		string fileName = prefix + definition.EntityName + "Installer.cs";
		string filePath = Path.Combine(outputDir, fileName);
		string contents = GenerateInstallerContent(definition, config, fileName, isScriptable: false, hasBothInstallers);
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
		StringBuilder sb = new StringBuilder();
		sb.Append(EntityDomainFileHelper.GetFileHeader(definition, fileName, config));
		sb.AppendLine();
		sb.AppendLine("using Atomic.Entities;");
		string[] imports = definition.GetImports();
		foreach (string importEntry in imports)
		{
			if (!string.IsNullOrWhiteSpace(importEntry))
			{
				string trimmedImport = importEntry.Trim();
				sb.AppendLine(trimmedImport.StartsWith("using") ? trimmedImport : ("using " + trimmedImport + ";"));
			}
		}
		sb.AppendLine();
		sb.AppendLine($"namespace {definition.Namespace}");
		sb.AppendLine("{");
		sb.AppendLine("    /// <summary>");
		if (isScriptable)
		{
			sb.AppendLine($"    /// A Unity <see cref=\"ScriptableObject\"/> that defines reusable logic for installing or configuring an <see cref=\"I{definition.EntityName}\"/>.");
			sb.AppendLine("    /// </summary>");
			sb.AppendLine("    /// <remarks>");
			sb.AppendLine("    /// This is useful for defining shared configuration logic that can be applied to multiple entities,");
			sb.AppendLine("    /// such as setting default values, tags, or attaching behaviors.");
			sb.AppendLine("    /// Supports both runtime and edit-time contexts via utility methods.");
		}
		else
		{
			sb.AppendLine($"    /// A Unity <see cref=\"MonoBehaviour\"/> that can be attached to a GameObject to perform installation logic on an <see cref=\"I{definition.EntityName}\"/> during runtime or initialization.");
			sb.AppendLine("    /// </summary>");
			sb.AppendLine("    /// <remarks>");
			sb.AppendLine("    /// Used to declaratively configure entities placed in a scene.");
			sb.AppendLine("    /// In the Editor, it supports automatic refresh via <c>OnValidate</c>.");
		}
		sb.AppendLine("    /// </remarks>");
		string classPrefix = ((!usePrefixes) ? "" : (isScriptable ? "Scriptable" : "Scene"));
		string baseClassName = (isScriptable ? "ScriptableEntityInstaller" : "SceneEntityInstaller");
		sb.AppendLine($"    public abstract class {classPrefix}{definition.EntityName}Installer : {baseClassName}<I{definition.EntityName}>");
		sb.AppendLine("    {");
		sb.AppendLine("    }");
		sb.AppendLine("}");
		return sb.ToString();
	}

	private static string GenerateInstallerInterfaceContent(EntityDomainDefinition definition, CodeGenConfig config, string fileName)
	{
		StringBuilder sb = new StringBuilder();
		sb.Append(EntityDomainFileHelper.GetFileHeader(definition, fileName, config));
		sb.AppendLine();
		sb.AppendLine("using Atomic.Entities;");
		string[] imports = definition.GetImports();
		foreach (string importEntry in imports)
		{
			if (!string.IsNullOrWhiteSpace(importEntry))
			{
				string trimmedImport = importEntry.Trim();
				sb.AppendLine(trimmedImport.StartsWith("using") ? trimmedImport : ("using " + trimmedImport + ";"));
			}
		}
		sb.AppendLine();
		sb.AppendLine($"namespace {definition.Namespace}");
		sb.AppendLine("{");
		sb.AppendLine("    /// <summary>");
		sb.AppendLine($"    /// An interface contract for installers that configure <see cref=\"I{definition.EntityName}\"/> entities.");
		sb.AppendLine("    /// </summary>");
		sb.AppendLine("    /// <remarks>");
		sb.AppendLine("    /// Implement this interface to define custom installation logic that can be applied to entities.");
		sb.AppendLine("    /// This provides a common contract for both scriptable and scene-based installers.");
		sb.AppendLine("    /// </remarks>");
		sb.AppendLine($"    public interface I{definition.EntityName}Installer : IEntityInstaller<I{definition.EntityName}>");
		sb.AppendLine("    {");
		sb.AppendLine("    }");
		sb.AppendLine("}");
		return sb.ToString();
	}
}
