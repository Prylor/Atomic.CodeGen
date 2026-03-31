using System.IO;
using System.Text;
using System.Threading.Tasks;
using Atomic.CodeGen.Core.Models;
using Atomic.CodeGen.Core.Models.EntityDomain;
using Atomic.CodeGen.Utils;

namespace Atomic.CodeGen.Core.Generators.EntityDomain;

public static class EntityConcreteGenerator
{
	public static async Task GenerateAsync(EntityDomainDefinition definition, CodeGenConfig config, string outputDir)
	{
		string fileName = definition.EntityName + ".cs";
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
		if (definition.IsPureEntityMode())
		{
			sb.AppendLine("using System.Collections.Generic;");
		}
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
		AppendClassDocumentation(sb, definition);
		string baseClass = GetBaseClass(definition);
		sb.AppendLine($"    public sealed class {definition.EntityName} : {baseClass}, I{definition.EntityName}");
		sb.AppendLine("    {");
		if (definition.IsPureEntityMode())
		{
			AppendConstructors(sb, definition);
		}
		sb.AppendLine("    }");
		sb.AppendLine("}");
		return sb.ToString();
	}

	private static void AppendClassDocumentation(StringBuilder sb, EntityDomainDefinition definition)
	{
		sb.AppendLine("    /// <summary>");
		switch (definition.Mode)
		{
		case EntityMode.Entity:
			sb.AppendLine($"    /// Represents the core implementation of an <see cref=\"I{definition.EntityName}\"/> in the framework.");
			sb.AppendLine("    /// This class follows the Entity–State–Behaviour pattern, providing a modular container");
			sb.AppendLine("    /// for dynamic state, tags, behaviours, and lifecycle management.");
			break;
		case EntityMode.EntitySingleton:
			sb.AppendLine("    /// Abstract base class for singleton entities.");
			sb.AppendLine("    /// Ensures a single globally accessible instance of type <typeparamref name=\"E\"/>.");
			sb.AppendLine("    /// Supports both default constructor and factory-based creation.");
			break;
		case EntityMode.SceneEntity:
			sb.AppendLine($"    /// Represents a Unity <see cref=\"SceneEntity\"/> implementation for <see cref=\"I{definition.EntityName}\"/>.");
			sb.AppendLine("    /// This component can be instantiated directly in a Scene and composed via the Unity Inspector.");
			break;
		case EntityMode.SceneEntitySingleton:
			sb.AppendLine("    /// A base class for singleton scene entities. Ensures a single instance of the entity exists");
			sb.AppendLine("    /// per scene or globally, depending on the <see cref=\"_dontDestroyOnLoad\"/> flag.");
			break;
		}
		sb.AppendLine("    /// </summary>");
	}

	private static string GetBaseClass(EntityDomainDefinition definition)
	{
		return definition.Mode switch
		{
			EntityMode.Entity => "Entity",
			EntityMode.EntitySingleton => "EntitySingleton<" + definition.EntityName + ">",
			EntityMode.SceneEntity => "SceneEntity",
			EntityMode.SceneEntitySingleton => "SceneEntitySingleton<" + definition.EntityName + ">",
			_ => "Entity",
		};
	}

	private static void AppendConstructors(StringBuilder sb, EntityDomainDefinition definition)
	{
		if (definition.Mode == EntityMode.EntitySingleton)
		{
			sb.AppendLine($"        public {definition.EntityName}()");
			sb.AppendLine("        {");
			sb.AppendLine("        }");
			sb.AppendLine();
		}
		sb.AppendLine("        /// <summary>");
		sb.AppendLine("        /// Creates a new entity with the specified name, tags, values, behaviours, and optional settings.");
		sb.AppendLine("        /// </summary>");
		sb.AppendLine($"        public {definition.EntityName}(");
		sb.AppendLine("            string name,");
		sb.AppendLine("            IEnumerable<string> tags,");
		sb.AppendLine("            IEnumerable<KeyValuePair<string, object>> values,");
		sb.AppendLine("            IEnumerable<IEntityBehaviour> behaviours,");
		sb.AppendLine("            Settings? settings = null");
		sb.AppendLine("        ) : base(name, tags, values, behaviours, settings)");
		sb.AppendLine("        {");
		sb.AppendLine("        }");
		sb.AppendLine();
		sb.AppendLine("        /// <summary>");
		sb.AppendLine("        /// Creates a new entity with the specified name, tags, values, behaviours, and optional settings.");
		sb.AppendLine("        /// </summary>");
		sb.AppendLine($"        public {definition.EntityName}(");
		sb.AppendLine("            string name,");
		sb.AppendLine("            IEnumerable<int> tags,");
		sb.AppendLine("            IEnumerable<KeyValuePair<int, object>> values,");
		sb.AppendLine("            IEnumerable<IEntityBehaviour> behaviours,");
		sb.AppendLine("            Settings? settings = null");
		sb.AppendLine("        ) : base(name, tags, values, behaviours, settings)");
		sb.AppendLine("        {");
		sb.AppendLine("        }");
		sb.AppendLine();
		sb.AppendLine("        /// <summary>");
		sb.AppendLine("        /// Creates a new entity with the specified name and initial capacities for tags, values, and behaviours.");
		sb.AppendLine("        /// </summary>");
		sb.AppendLine($"        public {definition.EntityName}(");
		sb.AppendLine("            string name = null,");
		sb.AppendLine("            int tagCapacity = 0,");
		sb.AppendLine("            int valueCapacity = 0,");
		sb.AppendLine("            int behaviourCapacity = 0,");
		sb.AppendLine("            Settings? settings = null");
		sb.AppendLine("        ) : base(name, tagCapacity, valueCapacity, behaviourCapacity, settings)");
		sb.AppendLine("        {");
		sb.AppendLine("        }");
	}
}
