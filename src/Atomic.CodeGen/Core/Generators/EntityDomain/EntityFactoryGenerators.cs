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
			handler = new StringBuilder.AppendInterpolatedStringHandler(109, 1, stringBuilder2);
			handler.AppendLiteral("    /// A Unity <see cref=\"ScriptableObject\"/> factory for creating and configuring <see cref=\"I");
			handler.AppendFormatted(definition.EntityName);
			handler.AppendLiteral("\"/> entities.");
			stringBuilder4.AppendLine(ref handler);
			stringBuilder.AppendLine("    /// </summary>");
			stringBuilder.AppendLine("    /// <remarks>");
			stringBuilder.AppendLine("    /// This factory can be used as a reusable asset to instantiate entities with predefined settings.");
			stringBuilder.AppendLine("    /// Override the <see cref=\"Install\"/> method to customize entity initialization.");
		}
		else
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder5 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(119, 1, stringBuilder2);
			handler.AppendLiteral("    /// A Unity <see cref=\"MonoBehaviour\"/> factory for creating and configuring <see cref=\"I");
			handler.AppendFormatted(definition.EntityName);
			handler.AppendLiteral("\"/> entities in the scene.");
			stringBuilder5.AppendLine(ref handler);
			stringBuilder.AppendLine("    /// </summary>");
			stringBuilder.AppendLine("    /// <remarks>");
			stringBuilder.AppendLine("    /// This factory can be attached to a GameObject to provide scene-based entity creation.");
			stringBuilder.AppendLine("    /// Override the <see cref=\"Install\"/> method to customize entity initialization.");
		}
		stringBuilder.AppendLine("    /// </remarks>");
		string value = ((!usePrefixes) ? "" : (isScriptable ? "Scriptable" : "Scene"));
		string value2 = (isScriptable ? "ScriptableEntityFactory" : "SceneEntityFactory");
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder6 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(39, 4, stringBuilder2);
		handler.AppendLiteral("    public abstract class ");
		handler.AppendFormatted(value);
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral("Factory : ");
		handler.AppendFormatted(value2);
		handler.AppendLiteral("<I");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral(">");
		stringBuilder6.AppendLine(ref handler);
		stringBuilder.AppendLine("    {");
		stringBuilder.AppendLine("        public string Name => this.name;");
		stringBuilder.AppendLine();
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder7 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(41, 1, stringBuilder2);
		handler.AppendLiteral("        public sealed override I");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral(" Create()");
		stringBuilder7.AppendLine(ref handler);
		stringBuilder.AppendLine("        {");
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder8 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(30, 1, stringBuilder2);
		handler.AppendLiteral("            var entity = new ");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral("(");
		stringBuilder8.AppendLine(ref handler);
		stringBuilder.AppendLine("                this.Name,");
		stringBuilder.AppendLine("                this.initialTagCapacity,");
		stringBuilder.AppendLine("                this.initialValueCapacity,");
		stringBuilder.AppendLine("                this.initialBehaviourCapacity");
		stringBuilder.AppendLine("            );");
		stringBuilder.AppendLine("            this.Install(entity);");
		stringBuilder.AppendLine("            return entity;");
		stringBuilder.AppendLine("        }");
		stringBuilder.AppendLine();
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder9 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(50, 1, stringBuilder2);
		handler.AppendLiteral("        protected abstract void Install(I");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral(" entity);");
		stringBuilder9.AppendLine(ref handler);
		stringBuilder.AppendLine("    }");
		stringBuilder.AppendLine("}");
		return stringBuilder.ToString();
	}
}
