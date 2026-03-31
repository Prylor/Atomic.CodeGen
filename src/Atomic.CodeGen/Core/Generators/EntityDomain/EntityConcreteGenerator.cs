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
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append(EntityDomainFileHelper.GetFileHeader(definition, fileName, config));
		stringBuilder.AppendLine();
		stringBuilder.AppendLine("using Atomic.Entities;");
		if (definition.IsPureEntityMode())
		{
			stringBuilder.AppendLine("using System.Collections.Generic;");
		}
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
		AppendClassDocumentation(stringBuilder, definition);
		string baseClass = GetBaseClass(definition);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder4 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(30, 3, stringBuilder2);
		handler.AppendLiteral("    public sealed class ");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral(" : ");
		handler.AppendFormatted(baseClass);
		handler.AppendLiteral(", I");
		handler.AppendFormatted(definition.EntityName);
		stringBuilder4.AppendLine(ref handler);
		stringBuilder.AppendLine("    {");
		if (definition.IsPureEntityMode())
		{
			AppendConstructors(stringBuilder, definition);
		}
		stringBuilder.AppendLine("    }");
		stringBuilder.AppendLine("}");
		return stringBuilder.ToString();
	}

	private static void AppendClassDocumentation(StringBuilder sb, EntityDomainDefinition definition)
	{
		sb.AppendLine("    /// <summary>");
		switch (definition.Mode)
		{
		case EntityMode.Entity:
		{
			StringBuilder stringBuilder = sb;
			StringBuilder stringBuilder3 = stringBuilder;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(82, 1, stringBuilder);
			handler.AppendLiteral("    /// Represents the core implementation of an <see cref=\"I");
			handler.AppendFormatted(definition.EntityName);
			handler.AppendLiteral("\"/> in the framework.");
			stringBuilder3.AppendLine(ref handler);
			sb.AppendLine("    /// This class follows the Entity–State–Behaviour pattern, providing a modular container");
			sb.AppendLine("    /// for dynamic state, tags, behaviours, and lifecycle management.");
			break;
		}
		case EntityMode.EntitySingleton:
			sb.AppendLine("    /// Abstract base class for singleton entities.");
			sb.AppendLine("    /// Ensures a single globally accessible instance of type <typeparamref name=\"E\"/>.");
			sb.AppendLine("    /// Supports both default constructor and factory-based creation.");
			break;
		case EntityMode.SceneEntity:
		{
			StringBuilder stringBuilder = sb;
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(88, 1, stringBuilder);
			handler.AppendLiteral("    /// Represents a Unity <see cref=\"SceneEntity\"/> implementation for <see cref=\"I");
			handler.AppendFormatted(definition.EntityName);
			handler.AppendLiteral("\"/>.");
			stringBuilder2.AppendLine(ref handler);
			sb.AppendLine("    /// This component can be instantiated directly in a Scene and composed via the Unity Inspector.");
			break;
		}
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
		StringBuilder stringBuilder;
		StringBuilder.AppendInterpolatedStringHandler handler;
		if (definition.Mode == EntityMode.EntitySingleton)
		{
			stringBuilder = sb;
			StringBuilder stringBuilder2 = stringBuilder;
			handler = new StringBuilder.AppendInterpolatedStringHandler(17, 1, stringBuilder);
			handler.AppendLiteral("        public ");
			handler.AppendFormatted(definition.EntityName);
			handler.AppendLiteral("()");
			stringBuilder2.AppendLine(ref handler);
			sb.AppendLine("        {");
			sb.AppendLine("        }");
			sb.AppendLine();
		}
		sb.AppendLine("        /// <summary>");
		sb.AppendLine("        /// Creates a new entity with the specified name, tags, values, behaviours, and optional settings.");
		sb.AppendLine("        /// </summary>");
		stringBuilder = sb;
		StringBuilder stringBuilder3 = stringBuilder;
		handler = new StringBuilder.AppendInterpolatedStringHandler(16, 1, stringBuilder);
		handler.AppendLiteral("        public ");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral("(");
		stringBuilder3.AppendLine(ref handler);
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
		stringBuilder = sb;
		StringBuilder stringBuilder4 = stringBuilder;
		handler = new StringBuilder.AppendInterpolatedStringHandler(16, 1, stringBuilder);
		handler.AppendLiteral("        public ");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral("(");
		stringBuilder4.AppendLine(ref handler);
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
		stringBuilder = sb;
		StringBuilder stringBuilder5 = stringBuilder;
		handler = new StringBuilder.AppendInterpolatedStringHandler(16, 1, stringBuilder);
		handler.AppendLiteral("        public ");
		handler.AppendFormatted(definition.EntityName);
		handler.AppendLiteral("(");
		stringBuilder5.AppendLine(ref handler);
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
