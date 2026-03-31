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
		bool flag = definition.Factories.HasFlag(EntityFactoryMode.ScriptableEntityFactory) && definition.Factories.HasFlag(EntityFactoryMode.SceneEntityFactory);
		string text = (flag ? "Scriptable" : "");
		string fileName = text + definition.EntityName + "Factory.cs";
		string filePath = Path.Combine(outputDir, fileName);
		string contents = GenerateFactoryContent(definition, config, fileName, isScriptable: true, flag);
		await File.WriteAllTextAsync(filePath, contents);
		await EntityDomainFileHelper.GenerateMetaFileAsync(filePath);
		await EntityDomainFileHelper.LinkToProjectsAsync(definition, config, filePath);
		Logger.LogVerbose("Generated: " + fileName);
	}

	public static async Task GenerateSceneFactoryAsync(EntityDomainDefinition definition, CodeGenConfig config, string outputDir)
	{
		bool flag = definition.Factories.HasFlag(EntityFactoryMode.ScriptableEntityFactory) && definition.Factories.HasFlag(EntityFactoryMode.SceneEntityFactory);
		string text = (flag ? "Scene" : "");
		string fileName = text + definition.EntityName + "Factory.cs";
		string filePath = Path.Combine(outputDir, fileName);
		string contents = GenerateFactoryContent(definition, config, fileName, isScriptable: false, flag);
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
		string value = ((!usePrefixes) ? "" : (isScriptable ? "Scriptable" : "Scene"));
		string value2 = (isScriptable ? "ScriptableEntityFactory" : "SceneEntityFactory");
		sb.AppendLine($"    public abstract class {value}{definition.EntityName}Factory : {value2}<I{definition.EntityName}>");
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
