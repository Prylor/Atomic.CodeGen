using System.IO;
using System.Text;
using System.Threading.Tasks;
using Atomic.CodeGen.Core.Models;
using Atomic.CodeGen.Core.Models.EntityDomain;
using Atomic.CodeGen.Utils;

namespace Atomic.CodeGen.Core.Generators.EntityDomain;

public static class EntityBehaviourGenerator
{
	private static readonly string[] EventNames = ["Init", "Enable", "Disable", "Dispose", "Tick", "FixedTick", "LateTick", "Gizmos"];

	private static readonly string[] BaseInterfaces = ["IEntityInit", "IEntityEnable", "IEntityDisable", "IEntityDispose", "IEntityTick", "IEntityFixedTick", "IEntityLateTick", "IEntityGizmos"];

	private static readonly string[] Summaries = ["Provides initialization logic for the strongly-typed <see cref=\"{0}\"/> entity.", "Handles enable-time logic for the strongly-typed <see cref=\"{0}\"/> entity.", "Handles disable-time logic for the strongly-typed <see cref=\"{0}\"/> entity.", "Provides cleanup logic for the strongly-typed <see cref=\"{0}\"/> entity.", "Handles per-frame update logic for the strongly-typed <see cref=\"{0}\"/> entity.", "Handles fixed update logic for the strongly-typed <see cref=\"{0}\"/> entity.", "Handles late update logic for the strongly-typed <see cref=\"{0}\"/> entity.", "Provides editor visualization logic for the strongly-typed <see cref=\"{0}\"/> entity."];

	private static readonly string[] Remarks = ["Automatically invoked when an <see cref=\"{0}\"/> instance is created and enters the initialization phase.\nTypically used to set up component references, register event listeners, or assign default values.", "Automatically invoked when an <see cref=\"{0}\"/> instance becomes active or enabled.\nCommonly used to re-enable systems or resume behavior execution.", "Automatically invoked when an <see cref=\"{0}\"/> instance becomes inactive or disabled.\nUseful for pausing updates or temporarily suspending logic without disposing the entity.", "Automatically called when an <see cref=\"{0}\"/> instance is destroyed or disposed.\nUsed to release resources, unsubscribe from events, or reset state.", "Automatically invoked during the main update loop.\nTypically used for time-dependent gameplay logic such as movement, state updates, or input processing.", "Automatically invoked during Unity's fixed update cycle, synchronized with the physics system.\nCommonly used for deterministic or physics-based updates.", "Automatically invoked after all standard update calls within the frame.\nTypically used for camera adjustments, cleanup, or visual synchronization logic.", "Automatically invoked when the entity is visible in the Unity Editor Scene view.\nCommonly used to draw debug information, wireframes, or gizmo markers."];

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
		for (int j = 0; j < BaseInterfaces.Length; j++)
		{
			string summary = string.Format(Summaries[j], definition.EntityName);
			string remarks = string.Format(Remarks[j], definition.EntityName);
			sb.AppendLine("    /// <summary>");
			sb.AppendLine($"    /// {summary}");
			sb.AppendLine("    /// </summary>");
			sb.AppendLine("    /// <remarks>");
			imports = remarks.Split('\n');
			foreach (string remarkLine in imports)
			{
				sb.AppendLine($"    /// {remarkLine}");
			}
			sb.AppendLine("    /// </remarks>");
			sb.AppendLine($"    public interface {definition.EntityName}{EventNames[j]} : {BaseInterfaces[j]}<{definition.EntityName}>");
			sb.AppendLine("    {");
			sb.AppendLine("    }");
			if (j < BaseInterfaces.Length - 1)
			{
				sb.AppendLine();
			}
		}
		sb.AppendLine("}");
		return sb.ToString();
	}
}
