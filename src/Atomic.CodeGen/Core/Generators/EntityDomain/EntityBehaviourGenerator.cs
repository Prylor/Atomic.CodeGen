using System.IO;
using System.Text;
using System.Threading.Tasks;
using Atomic.CodeGen.Core.Models;
using Atomic.CodeGen.Core.Models.EntityDomain;
using Atomic.CodeGen.Utils;

namespace Atomic.CodeGen.Core.Generators.EntityDomain;

public static class EntityBehaviourGenerator
{
	private static readonly string[] EventNames = new string[8] { "Init", "Enable", "Disable", "Dispose", "Tick", "FixedTick", "LateTick", "Gizmos" };

	private static readonly string[] BaseInterfaces = new string[8] { "IEntityInit", "IEntityEnable", "IEntityDisable", "IEntityDispose", "IEntityTick", "IEntityFixedTick", "IEntityLateTick", "IEntityGizmos" };

	private static readonly string[] Summaries = new string[8] { "Provides initialization logic for the strongly-typed <see cref=\"{0}\"/> entity.", "Handles enable-time logic for the strongly-typed <see cref=\"{0}\"/> entity.", "Handles disable-time logic for the strongly-typed <see cref=\"{0}\"/> entity.", "Provides cleanup logic for the strongly-typed <see cref=\"{0}\"/> entity.", "Handles per-frame update logic for the strongly-typed <see cref=\"{0}\"/> entity.", "Handles fixed update logic for the strongly-typed <see cref=\"{0}\"/> entity.", "Handles late update logic for the strongly-typed <see cref=\"{0}\"/> entity.", "Provides editor visualization logic for the strongly-typed <see cref=\"{0}\"/> entity." };

	private static readonly string[] Remarks = new string[8] { "Automatically invoked when an <see cref=\"{0}\"/> instance is created and enters the initialization phase.\nTypically used to set up component references, register event listeners, or assign default values.", "Automatically invoked when an <see cref=\"{0}\"/> instance becomes active or enabled.\nCommonly used to re-enable systems or resume behavior execution.", "Automatically invoked when an <see cref=\"{0}\"/> instance becomes inactive or disabled.\nUseful for pausing updates or temporarily suspending logic without disposing the entity.", "Automatically called when an <see cref=\"{0}\"/> instance is destroyed or disposed.\nUsed to release resources, unsubscribe from events, or reset state.", "Automatically invoked during the main update loop.\nTypically used for time-dependent gameplay logic such as movement, state updates, or input processing.", "Automatically invoked during Unity's fixed update cycle, synchronized with the physics system.\nCommonly used for deterministic or physics-based updates.", "Automatically invoked after all standard update calls within the frame.\nTypically used for camera adjustments, cleanup, or visual synchronization logic.", "Automatically invoked when the entity is visible in the Unity Editor Scene view.\nCommonly used to draw debug information, wireframes, or gizmo markers." };

	public static async Task GenerateAsync(EntityDomainDefinition definition, CodeGenConfig config, string outputDir)
	{
		string fileName = definition.EntityName + "Behaviours.cs";
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
		for (int j = 0; j < BaseInterfaces.Length; j++)
		{
			string value = string.Format(Summaries[j], definition.EntityName);
			string text3 = string.Format(Remarks[j], definition.EntityName);
			stringBuilder.AppendLine("    /// <summary>");
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder4 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(8, 1, stringBuilder2);
			handler.AppendLiteral("    /// ");
			handler.AppendFormatted(value);
			stringBuilder4.AppendLine(ref handler);
			stringBuilder.AppendLine("    /// </summary>");
			stringBuilder.AppendLine("    /// <remarks>");
			imports = text3.Split('\n');
			foreach (string value2 in imports)
			{
				stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder5 = stringBuilder2;
				handler = new StringBuilder.AppendInterpolatedStringHandler(8, 1, stringBuilder2);
				handler.AppendLiteral("    /// ");
				handler.AppendFormatted(value2);
				stringBuilder5.AppendLine(ref handler);
			}
			stringBuilder.AppendLine("    /// </remarks>");
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder6 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(26, 4, stringBuilder2);
			handler.AppendLiteral("    public interface ");
			handler.AppendFormatted(definition.EntityName);
			handler.AppendFormatted(EventNames[j]);
			handler.AppendLiteral(" : ");
			handler.AppendFormatted(BaseInterfaces[j]);
			handler.AppendLiteral("<");
			handler.AppendFormatted(definition.EntityName);
			handler.AppendLiteral(">");
			stringBuilder6.AppendLine(ref handler);
			stringBuilder.AppendLine("    {");
			stringBuilder.AppendLine("    }");
			if (j < BaseInterfaces.Length - 1)
			{
				stringBuilder.AppendLine();
			}
		}
		stringBuilder.AppendLine("}");
		return stringBuilder.ToString();
	}
}
