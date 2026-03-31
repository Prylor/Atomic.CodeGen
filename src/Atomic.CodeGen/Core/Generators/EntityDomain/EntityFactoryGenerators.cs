using System.IO;
using System.Text;
using System.Threading.Tasks;
using Atomic.CodeGen.Core.Models;
using Atomic.CodeGen.Core.Models.EntityDomain;
using Atomic.CodeGen.Utils;

namespace Atomic.CodeGen.Core.Generators.EntityDomain;

public static class EntityFactoryGenerators
{
	public static async Task GenerateScriptableFactoryAsync(EntityDomainDefinition definition, CodeGenConfig config, string outputDir)
	{
		bool hasBothFactories = definition.Factories.HasFlag(EntityFactoryMode.ScriptableEntityFactory) && definition.Factories.HasFlag(EntityFactoryMode.SceneEntityFactory);
		string prefix = (hasBothFactories ? "Scriptable" : "");
		string fileName = prefix + definition.EntityName + "Factory.cs";
		string filePath = Path.Combine(outputDir, fileName);
		string contents = GenerateFactoryContent(definition, config, fileName, isScriptable: true, hasBothFactories);
		await File.WriteAllTextAsync(filePath, contents);
		await EntityDomainFileHelper.GenerateMetaFileAsync(filePath);
		await EntityDomainFileHelper.LinkToProjectsAsync(definition, config, filePath);
		Logger.LogVerbose("Generated: " + fileName);
	}

	public static async Task GenerateSceneFactoryAsync(EntityDomainDefinition definition, CodeGenConfig config, string outputDir)
	{
		bool hasBothFactories = definition.Factories.HasFlag(EntityFactoryMode.ScriptableEntityFactory) && definition.Factories.HasFlag(EntityFactoryMode.SceneEntityFactory);
		string prefix = (hasBothFactories ? "Scene" : "");
		string fileName = prefix + definition.EntityName + "Factory.cs";
		string filePath = Path.Combine(outputDir, fileName);
		string contents = GenerateFactoryContent(definition, config, fileName, isScriptable: false, hasBothFactories);
		await File.WriteAllTextAsync(filePath, contents);
		await EntityDomainFileHelper.GenerateMetaFileAsync(filePath);
		await EntityDomainFileHelper.LinkToProjectsAsync(definition, config, filePath);
		Logger.LogVerbose("Generated: " + fileName);
	}

	private static string GenerateFactoryContent(EntityDomainDefinition definition, CodeGenConfig config, string fileName, bool isScriptable, bool usePrefixes)
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
			sb.AppendLine($"    /// A Unity <see cref=\"ScriptableObject\"/> factory for creating and configuring <see cref=\"I{definition.EntityName}\"/> entities.");
			sb.AppendLine("    /// </summary>");
			sb.AppendLine("    /// <remarks>");
			sb.AppendLine("    /// This factory can be used as a reusable asset to instantiate entities with predefined settings.");
			sb.AppendLine("    /// Override the <see cref=\"Install\"/> method to customize entity initialization.");
		}
		else
		{
			sb.AppendLine($"    /// A Unity <see cref=\"MonoBehaviour\"/> factory for creating and configuring <see cref=\"I{definition.EntityName}\"/> entities in the scene.");
			sb.AppendLine("    /// </summary>");
			sb.AppendLine("    /// <remarks>");
			sb.AppendLine("    /// This factory can be attached to a GameObject to provide scene-based entity creation.");
			sb.AppendLine("    /// Override the <see cref=\"Install\"/> method to customize entity initialization.");
		}
		sb.AppendLine("    /// </remarks>");
		string classPrefix = ((!usePrefixes) ? "" : (isScriptable ? "Scriptable" : "Scene"));
		string baseClassName = (isScriptable ? "ScriptableEntityFactory" : "SceneEntityFactory");
		sb.AppendLine($"    public abstract class {classPrefix}{definition.EntityName}Factory : {baseClassName}<I{definition.EntityName}>");
		sb.AppendLine("    {");
		sb.AppendLine("        public string Name => this.name;");
		sb.AppendLine();
		sb.AppendLine($"        public sealed override I{definition.EntityName} Create()");
		sb.AppendLine("        {");
		sb.AppendLine($"            var entity = new {definition.EntityName}(");
		sb.AppendLine("                this.Name,");
		sb.AppendLine("                this.initialTagCapacity,");
		sb.AppendLine("                this.initialValueCapacity,");
		sb.AppendLine("                this.initialBehaviourCapacity");
		sb.AppendLine("            );");
		sb.AppendLine("            this.Install(entity);");
		sb.AppendLine("            return entity;");
		sb.AppendLine("        }");
		sb.AppendLine();
		sb.AppendLine($"        protected abstract void Install(I{definition.EntityName} entity);");
		sb.AppendLine("    }");
		sb.AppendLine("}");
		return sb.ToString();
	}
}
