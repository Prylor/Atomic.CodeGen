using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Atomic.CodeGen.Core.Models;
using Atomic.CodeGen.Utils;

namespace Atomic.CodeGen.Core;

public sealed class EntityAPIGenerator
{
	private const string AggressiveInliningAttribute = "[MethodImpl(MethodImplOptions.AggressiveInlining)]";

	private const string UnsafeSuffix = "Unsafe";

	private const string RefModifier = "ref";

	private const string ParamName = "entity";

	private readonly EntityAPIDefinition _definition;

	private readonly CodeGenConfig _config;

	private readonly string _indent;

	public EntityAPIGenerator(EntityAPIDefinition definition, CodeGenConfig config)
	{
		_definition = definition;
		_config = config;
		_indent = config.Formatting.GetIndent();
	}

	public async Task<bool> GenerateAsync()
	{
		string outputPath = _definition.GetOutputFilePath(_config);
		string directoryName = Path.GetDirectoryName(outputPath);
		if (!string.IsNullOrEmpty(directoryName) && !Directory.Exists(directoryName))
		{
			Directory.CreateDirectory(directoryName);
			Logger.LogVerbose("Created directory: " + directoryName);
		}
		if (File.Exists(outputPath) && !(await IsOurGeneratedFileAsync(outputPath)))
		{
			Logger.LogError("Cannot generate " + _definition.ClassName + ": A non-generated file already exists at " + outputPath);
			Logger.LogError("Please remove the existing file or choose a different class name/directory.");
			return false;
		}
		string contents = GenerateContent();
		await File.WriteAllTextAsync(outputPath, contents);
		await GenerateMetaFileAsync(outputPath);
		List<string> list = new List<string>();
		if (!string.IsNullOrWhiteSpace(_definition.TargetProject))
		{
			list.Add(_definition.TargetProject);
		}
		else
		{
			List<string> list2 = ProjectDetector.FindProjectsForFile(_definition.SourceFile, _config.GetAbsoluteProjectRoot());
			if (list2.Count == 0)
			{
				Logger.LogWarning("Could not find any .csproj containing " + _definition.SourceFile + ". Generated file will not be linked to any project.");
			}
			else
			{
				list.AddRange(list2);
			}
		}
		foreach (string item in list)
		{
			await ProjectFileManager.AddGeneratedFileAsync(Path.Combine(_config.GetAbsoluteProjectRoot(), item), outputPath, _config.GetAbsoluteProjectRoot());
		}
		Logger.LogSuccess("Generated: " + outputPath);
		return true;
	}

	private async Task GenerateMetaFileAsync(string csFilePath)
	{
		string metaPath = csFilePath + ".meta";
		string text = GenerateGuidFromPath(csFilePath);
		string contents = "fileFormatVersion: 2\r\nguid: " + text + "\r\nMonoImporter:\r\n  externalObjects: {}\r\n  serializedVersion: 2\r\n  defaultReferences: []\r\n  executionOrder: 0\r\n  icon: {instanceID: 0}\r\n  userData:\r\n  assetBundleName:\r\n  assetBundleVariant:\r\n";
		await File.WriteAllTextAsync(metaPath, contents);
		Logger.LogVerbose("Generated meta file: " + metaPath);
	}

	private static string GenerateGuidFromPath(string path)
	{
		using MD5 mD = MD5.Create();
		byte[] b = mD.ComputeHash(Encoding.UTF8.GetBytes(path));
		return new Guid(b).ToString("N");
	}

	private string GenerateContent()
	{
		StringBuilder stringBuilder = new StringBuilder();
		bool num = !string.IsNullOrWhiteSpace(_definition.Namespace);
		string text = (num ? _indent : "");
		string indent = text + _indent;
		AppendHeader(stringBuilder);
		AppendUsings(stringBuilder);
		stringBuilder.AppendLine();
		StringBuilder stringBuilder2;
		StringBuilder.AppendInterpolatedStringHandler handler;
		if (num)
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder3 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(10, 1, stringBuilder2);
			handler.AppendLiteral("namespace ");
			handler.AppendFormatted(_definition.Namespace);
			stringBuilder3.AppendLine(ref handler);
			stringBuilder.AppendLine("{");
		}
		stringBuilder.AppendLine("#if UNITY_EDITOR");
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder4 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(18, 1, stringBuilder2);
		handler.AppendFormatted(text);
		handler.AppendLiteral("[InitializeOnLoad]");
		stringBuilder4.AppendLine(ref handler);
		stringBuilder.AppendLine("#endif");
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder5 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(28, 2, stringBuilder2);
		handler.AppendFormatted(text);
		handler.AppendLiteral("public static partial class ");
		handler.AppendFormatted(_definition.ClassName);
		stringBuilder5.AppendLine(ref handler);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder6 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(1, 1, stringBuilder2);
		handler.AppendFormatted(text);
		handler.AppendLiteral("{");
		stringBuilder6.AppendLine(ref handler);
		if (_definition.Tags.Count > 0)
		{
			AppendTagFields(stringBuilder, indent);
		}
		if (_definition.Values.Count > 0)
		{
			AppendValueFields(stringBuilder, indent);
		}
		AppendStaticConstructor(stringBuilder, indent);
		if (_definition.Tags.Count > 0)
		{
			AppendTagExtensions(stringBuilder, indent);
		}
		if (_definition.Values.Count > 0)
		{
			AppendValueExtensions(stringBuilder, indent);
		}
		if (_definition.LinkedBehaviours.Count > 0)
		{
			AppendBehaviourExtensions(stringBuilder, indent);
		}
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder7 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(1, 1, stringBuilder2);
		handler.AppendFormatted(text);
		handler.AppendLiteral("}");
		stringBuilder7.AppendLine(ref handler);
		if (num)
		{
			stringBuilder.AppendLine("}");
		}
		return stringBuilder.ToString();
	}

	private void AppendHeader(StringBuilder sb)
	{
		sb.AppendLine("/**");
		sb.AppendLine(" * Code generation. Don't modify!");
		StringBuilder stringBuilder = sb;
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(19, 1, stringBuilder);
		handler.AppendLiteral(" * Generated from: ");
		handler.AppendFormatted(Path.GetFileName(_definition.SourceFile));
		stringBuilder2.AppendLine(ref handler);
		stringBuilder = sb;
		StringBuilder stringBuilder3 = stringBuilder;
		handler = new StringBuilder.AppendInterpolatedStringHandler(21, 1, stringBuilder);
		handler.AppendLiteral(" * Source file path: ");
		handler.AppendFormatted(_definition.SourceFile);
		stringBuilder3.AppendLine(ref handler);
		if (_config.IncludeTimestamp)
		{
			stringBuilder = sb;
			StringBuilder stringBuilder4 = stringBuilder;
			handler = new StringBuilder.AppendInterpolatedStringHandler(17, 1, stringBuilder);
			handler.AppendLiteral(" * Generated at: ");
			handler.AppendFormatted(DateTime.Now, "yyyy-MM-dd HH:mm:ss");
			stringBuilder4.AppendLine(ref handler);
		}
		if (_config.TrackOrphans)
		{
			sb.AppendLine(" * AtomicGenerator: track file");
		}
		sb.AppendLine(" **/");
		sb.AppendLine();
	}

	private void AppendUsings(StringBuilder sb)
	{
		sb.AppendLine("using Atomic.Entities;");
		sb.AppendLine("using static Atomic.Entities.EntityNames;");
		if (_definition.AggressiveInlining)
		{
			sb.AppendLine("using System.Runtime.CompilerServices;");
		}
		sb.AppendLine("#if UNITY_EDITOR");
		sb.AppendLine("using UnityEditor;");
		sb.AppendLine("#endif");
		foreach (string import in _definition.Imports)
		{
			if (!string.IsNullOrWhiteSpace(import))
			{
				StringBuilder stringBuilder = sb;
				StringBuilder stringBuilder2 = stringBuilder;
				StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(7, 1, stringBuilder);
				handler.AppendLiteral("using ");
				handler.AppendFormatted(import);
				handler.AppendLiteral(";");
				stringBuilder2.AppendLine(ref handler);
			}
		}
		foreach (string item in from ns in (from b in _definition.LinkedBehaviours
				where !string.IsNullOrWhiteSpace(b.Namespace)
				select b.Namespace).Distinct()
			where ns != _definition.Namespace && !_definition.Imports.Contains(ns)
			select ns)
		{
			StringBuilder stringBuilder = sb;
			StringBuilder stringBuilder3 = stringBuilder;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(7, 1, stringBuilder);
			handler.AppendLiteral("using ");
			handler.AppendFormatted(item);
			handler.AppendLiteral(";");
			stringBuilder3.AppendLine(ref handler);
		}
		foreach (string item2 in from imp in _definition.LinkedBehaviours.SelectMany((BehaviourDefinition b) => b.RequiredImports).Distinct()
			where imp != _definition.Namespace && !_definition.Imports.Contains(imp)
			select imp)
		{
			if (!string.IsNullOrWhiteSpace(item2))
			{
				StringBuilder stringBuilder = sb;
				StringBuilder stringBuilder4 = stringBuilder;
				StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(7, 1, stringBuilder);
				handler.AppendLiteral("using ");
				handler.AppendFormatted(item2);
				handler.AppendLiteral(";");
				stringBuilder4.AppendLine(ref handler);
			}
		}
	}

	private void AppendTagFields(StringBuilder sb, string indent)
	{
		sb.AppendLine();
		StringBuilder stringBuilder = sb;
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(7, 1, stringBuilder);
		handler.AppendFormatted(indent);
		handler.AppendLiteral("///Tags");
		stringBuilder2.AppendLine(ref handler);
		foreach (string tag in _definition.Tags)
		{
			stringBuilder = sb;
			StringBuilder stringBuilder3 = stringBuilder;
			handler = new StringBuilder.AppendInterpolatedStringHandler(28, 2, stringBuilder);
			handler.AppendFormatted(indent);
			handler.AppendLiteral("public static readonly int ");
			handler.AppendFormatted(tag);
			handler.AppendLiteral(";");
			stringBuilder3.AppendLine(ref handler);
		}
	}

	private void AppendValueFields(StringBuilder sb, string indent)
	{
		sb.AppendLine();
		StringBuilder stringBuilder = sb;
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(9, 1, stringBuilder);
		handler.AppendFormatted(indent);
		handler.AppendLiteral("///Values");
		stringBuilder2.AppendLine(ref handler);
		foreach (KeyValuePair<string, string> value4 in _definition.Values)
		{
			value4.Deconstruct(out var key, out var value);
			string value2 = key;
			string text = value;
			string value3 = (IsObjectType(text) ? "" : (" // " + text));
			stringBuilder = sb;
			StringBuilder stringBuilder3 = stringBuilder;
			handler = new StringBuilder.AppendInterpolatedStringHandler(28, 3, stringBuilder);
			handler.AppendFormatted(indent);
			handler.AppendLiteral("public static readonly int ");
			handler.AppendFormatted(value2);
			handler.AppendLiteral(";");
			handler.AppendFormatted(value3);
			stringBuilder3.AppendLine(ref handler);
		}
	}

	private void AppendStaticConstructor(StringBuilder sb, string indent)
	{
		string value = indent + _indent;
		sb.AppendLine();
		StringBuilder stringBuilder = sb;
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(9, 2, stringBuilder);
		handler.AppendFormatted(indent);
		handler.AppendLiteral("static ");
		handler.AppendFormatted(_definition.ClassName);
		handler.AppendLiteral("()");
		stringBuilder2.AppendLine(ref handler);
		stringBuilder = sb;
		StringBuilder stringBuilder3 = stringBuilder;
		handler = new StringBuilder.AppendInterpolatedStringHandler(1, 1, stringBuilder);
		handler.AppendFormatted(indent);
		handler.AppendLiteral("{");
		stringBuilder3.AppendLine(ref handler);
		if (_definition.Tags.Count > 0)
		{
			stringBuilder = sb;
			StringBuilder stringBuilder4 = stringBuilder;
			handler = new StringBuilder.AppendInterpolatedStringHandler(6, 1, stringBuilder);
			handler.AppendFormatted(value);
			handler.AppendLiteral("//Tags");
			stringBuilder4.AppendLine(ref handler);
			foreach (string tag in _definition.Tags)
			{
				stringBuilder = sb;
				StringBuilder stringBuilder5 = stringBuilder;
				handler = new StringBuilder.AppendInterpolatedStringHandler(22, 3, stringBuilder);
				handler.AppendFormatted(value);
				handler.AppendFormatted(tag);
				handler.AppendLiteral(" = NameToId(nameof(");
				handler.AppendFormatted(tag);
				handler.AppendLiteral("));");
				stringBuilder5.AppendLine(ref handler);
			}
		}
		if (_definition.Values.Count > 0)
		{
			if (_definition.Tags.Count > 0)
			{
				sb.AppendLine();
			}
			stringBuilder = sb;
			StringBuilder stringBuilder6 = stringBuilder;
			handler = new StringBuilder.AppendInterpolatedStringHandler(8, 1, stringBuilder);
			handler.AppendFormatted(value);
			handler.AppendLiteral("//Values");
			stringBuilder6.AppendLine(ref handler);
			foreach (KeyValuePair<string, string> value4 in _definition.Values)
			{
				value4.Deconstruct(out var key, out var _);
				string value3 = key;
				stringBuilder = sb;
				StringBuilder stringBuilder7 = stringBuilder;
				handler = new StringBuilder.AppendInterpolatedStringHandler(22, 3, stringBuilder);
				handler.AppendFormatted(value);
				handler.AppendFormatted(value3);
				handler.AppendLiteral(" = NameToId(nameof(");
				handler.AppendFormatted(value3);
				handler.AppendLiteral("));");
				stringBuilder7.AppendLine(ref handler);
			}
		}
		stringBuilder = sb;
		StringBuilder stringBuilder8 = stringBuilder;
		handler = new StringBuilder.AppendInterpolatedStringHandler(1, 1, stringBuilder);
		handler.AppendFormatted(indent);
		handler.AppendLiteral("}");
		stringBuilder8.AppendLine(ref handler);
	}

	private void AppendTagExtensions(StringBuilder sb, string indent)
	{
		sb.AppendLine();
		sb.AppendLine();
		StringBuilder stringBuilder = sb;
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(17, 1, stringBuilder);
		handler.AppendFormatted(indent);
		handler.AppendLiteral("///Tag Extensions");
		stringBuilder2.AppendLine(ref handler);
		foreach (string tag in _definition.Tags)
		{
			sb.AppendLine();
			stringBuilder = sb;
			StringBuilder stringBuilder3 = stringBuilder;
			handler = new StringBuilder.AppendInterpolatedStringHandler(8, 2, stringBuilder);
			handler.AppendFormatted(indent);
			handler.AppendLiteral("#region ");
			handler.AppendFormatted(tag);
			stringBuilder3.AppendLine(ref handler);
			sb.AppendLine();
			AppendInliningAttribute(sb, indent);
			stringBuilder = sb;
			StringBuilder stringBuilder4 = stringBuilder;
			handler = new StringBuilder.AppendInterpolatedStringHandler(47, 6, stringBuilder);
			handler.AppendFormatted(indent);
			handler.AppendLiteral("public static bool Has");
			handler.AppendFormatted(tag);
			handler.AppendLiteral("Tag(this ");
			handler.AppendFormatted(_definition.EntityType);
			handler.AppendLiteral(" ");
			handler.AppendFormatted("entity");
			handler.AppendLiteral(") => ");
			handler.AppendFormatted("entity");
			handler.AppendLiteral(".HasTag(");
			handler.AppendFormatted(tag);
			handler.AppendLiteral(");");
			stringBuilder4.AppendLine(ref handler);
			sb.AppendLine();
			AppendInliningAttribute(sb, indent);
			stringBuilder = sb;
			StringBuilder stringBuilder5 = stringBuilder;
			handler = new StringBuilder.AppendInterpolatedStringHandler(47, 6, stringBuilder);
			handler.AppendFormatted(indent);
			handler.AppendLiteral("public static bool Add");
			handler.AppendFormatted(tag);
			handler.AppendLiteral("Tag(this ");
			handler.AppendFormatted(_definition.EntityType);
			handler.AppendLiteral(" ");
			handler.AppendFormatted("entity");
			handler.AppendLiteral(") => ");
			handler.AppendFormatted("entity");
			handler.AppendLiteral(".AddTag(");
			handler.AppendFormatted(tag);
			handler.AppendLiteral(");");
			stringBuilder5.AppendLine(ref handler);
			sb.AppendLine();
			AppendInliningAttribute(sb, indent);
			stringBuilder = sb;
			StringBuilder stringBuilder6 = stringBuilder;
			handler = new StringBuilder.AppendInterpolatedStringHandler(47, 6, stringBuilder);
			handler.AppendFormatted(indent);
			handler.AppendLiteral("public static bool Del");
			handler.AppendFormatted(tag);
			handler.AppendLiteral("Tag(this ");
			handler.AppendFormatted(_definition.EntityType);
			handler.AppendLiteral(" ");
			handler.AppendFormatted("entity");
			handler.AppendLiteral(") => ");
			handler.AppendFormatted("entity");
			handler.AppendLiteral(".DelTag(");
			handler.AppendFormatted(tag);
			handler.AppendLiteral(");");
			stringBuilder6.AppendLine(ref handler);
			sb.AppendLine();
			stringBuilder = sb;
			StringBuilder stringBuilder7 = stringBuilder;
			handler = new StringBuilder.AppendInterpolatedStringHandler(10, 1, stringBuilder);
			handler.AppendFormatted(indent);
			handler.AppendLiteral("#endregion");
			stringBuilder7.AppendLine(ref handler);
		}
	}

	private void AppendValueExtensions(StringBuilder sb, string indent)
	{
		sb.AppendLine();
		sb.AppendLine();
		StringBuilder stringBuilder = sb;
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(19, 1, stringBuilder);
		handler.AppendFormatted(indent);
		handler.AppendLiteral("///Value Extensions");
		stringBuilder2.AppendLine(ref handler);
		foreach (var (value, value2) in _definition.Values)
		{
			sb.AppendLine();
			stringBuilder = sb;
			StringBuilder stringBuilder3 = stringBuilder;
			handler = new StringBuilder.AppendInterpolatedStringHandler(8, 2, stringBuilder);
			handler.AppendFormatted(indent);
			handler.AppendLiteral("#region ");
			handler.AppendFormatted(value);
			stringBuilder3.AppendLine(ref handler);
			sb.AppendLine();
			string value3 = (_definition.UnsafeAccess ? "Unsafe" : "");
			string value4 = (_definition.UnsafeAccess ? "ref " : "");
			AppendInliningAttribute(sb, indent);
			stringBuilder = sb;
			StringBuilder stringBuilder4 = stringBuilder;
			handler = new StringBuilder.AppendInterpolatedStringHandler(44, 9, stringBuilder);
			handler.AppendFormatted(indent);
			handler.AppendLiteral("public static ");
			handler.AppendFormatted(value2);
			handler.AppendLiteral(" Get");
			handler.AppendFormatted(value);
			handler.AppendLiteral("(this ");
			handler.AppendFormatted(_definition.EntityType);
			handler.AppendLiteral(" ");
			handler.AppendFormatted("entity");
			handler.AppendLiteral(") => ");
			handler.AppendFormatted("entity");
			handler.AppendLiteral(".GetValue");
			handler.AppendFormatted(value3);
			handler.AppendLiteral("<");
			handler.AppendFormatted(value2);
			handler.AppendLiteral(">(");
			handler.AppendFormatted(value);
			handler.AppendLiteral(");");
			stringBuilder4.AppendLine(ref handler);
			if (_definition.UnsafeAccess)
			{
				sb.AppendLine();
				stringBuilder = sb;
				StringBuilder stringBuilder5 = stringBuilder;
				handler = new StringBuilder.AppendInterpolatedStringHandler(44, 11, stringBuilder);
				handler.AppendFormatted(indent);
				handler.AppendLiteral("public static ");
				handler.AppendFormatted(value4);
				handler.AppendFormatted(value2);
				handler.AppendLiteral(" Ref");
				handler.AppendFormatted(value);
				handler.AppendLiteral("(this ");
				handler.AppendFormatted(_definition.EntityType);
				handler.AppendLiteral(" ");
				handler.AppendFormatted("entity");
				handler.AppendLiteral(") => ");
				handler.AppendFormatted(value4);
				handler.AppendFormatted("entity");
				handler.AppendLiteral(".GetValue");
				handler.AppendFormatted(value3);
				handler.AppendLiteral("<");
				handler.AppendFormatted(value2);
				handler.AppendLiteral(">(");
				handler.AppendFormatted(value);
				handler.AppendLiteral(");");
				stringBuilder5.AppendLine(ref handler);
			}
			sb.AppendLine();
			AppendInliningAttribute(sb, indent);
			stringBuilder = sb;
			StringBuilder stringBuilder6 = stringBuilder;
			handler = new StringBuilder.AppendInterpolatedStringHandler(75, 8, stringBuilder);
			handler.AppendFormatted(indent);
			handler.AppendLiteral("public static bool TryGet");
			handler.AppendFormatted(value);
			handler.AppendLiteral("(this ");
			handler.AppendFormatted(_definition.EntityType);
			handler.AppendLiteral(" ");
			handler.AppendFormatted("entity");
			handler.AppendLiteral(", out ");
			handler.AppendFormatted(value2);
			handler.AppendLiteral(" value) => ");
			handler.AppendFormatted("entity");
			handler.AppendLiteral(".TryGetValue");
			handler.AppendFormatted(value3);
			handler.AppendLiteral("(");
			handler.AppendFormatted(value);
			handler.AppendLiteral(", out value);");
			stringBuilder6.AppendLine(ref handler);
			sb.AppendLine();
			AppendInliningAttribute(sb, indent);
			stringBuilder = sb;
			StringBuilder stringBuilder7 = stringBuilder;
			handler = new StringBuilder.AppendInterpolatedStringHandler(61, 7, stringBuilder);
			handler.AppendFormatted(indent);
			handler.AppendLiteral("public static void Add");
			handler.AppendFormatted(value);
			handler.AppendLiteral("(this ");
			handler.AppendFormatted(_definition.EntityType);
			handler.AppendLiteral(" ");
			handler.AppendFormatted("entity");
			handler.AppendLiteral(", ");
			handler.AppendFormatted(value2);
			handler.AppendLiteral(" value) => ");
			handler.AppendFormatted("entity");
			handler.AppendLiteral(".AddValue(");
			handler.AppendFormatted(value);
			handler.AppendLiteral(", value);");
			stringBuilder7.AppendLine(ref handler);
			sb.AppendLine();
			AppendInliningAttribute(sb, indent);
			stringBuilder = sb;
			StringBuilder stringBuilder8 = stringBuilder;
			handler = new StringBuilder.AppendInterpolatedStringHandler(46, 6, stringBuilder);
			handler.AppendFormatted(indent);
			handler.AppendLiteral("public static bool Has");
			handler.AppendFormatted(value);
			handler.AppendLiteral("(this ");
			handler.AppendFormatted(_definition.EntityType);
			handler.AppendLiteral(" ");
			handler.AppendFormatted("entity");
			handler.AppendLiteral(") => ");
			handler.AppendFormatted("entity");
			handler.AppendLiteral(".HasValue(");
			handler.AppendFormatted(value);
			handler.AppendLiteral(");");
			stringBuilder8.AppendLine(ref handler);
			sb.AppendLine();
			AppendInliningAttribute(sb, indent);
			stringBuilder = sb;
			StringBuilder stringBuilder9 = stringBuilder;
			handler = new StringBuilder.AppendInterpolatedStringHandler(46, 6, stringBuilder);
			handler.AppendFormatted(indent);
			handler.AppendLiteral("public static bool Del");
			handler.AppendFormatted(value);
			handler.AppendLiteral("(this ");
			handler.AppendFormatted(_definition.EntityType);
			handler.AppendLiteral(" ");
			handler.AppendFormatted("entity");
			handler.AppendLiteral(") => ");
			handler.AppendFormatted("entity");
			handler.AppendLiteral(".DelValue(");
			handler.AppendFormatted(value);
			handler.AppendLiteral(");");
			stringBuilder9.AppendLine(ref handler);
			sb.AppendLine();
			AppendInliningAttribute(sb, indent);
			stringBuilder = sb;
			StringBuilder stringBuilder10 = stringBuilder;
			handler = new StringBuilder.AppendInterpolatedStringHandler(61, 7, stringBuilder);
			handler.AppendFormatted(indent);
			handler.AppendLiteral("public static void Set");
			handler.AppendFormatted(value);
			handler.AppendLiteral("(this ");
			handler.AppendFormatted(_definition.EntityType);
			handler.AppendLiteral(" ");
			handler.AppendFormatted("entity");
			handler.AppendLiteral(", ");
			handler.AppendFormatted(value2);
			handler.AppendLiteral(" value) => ");
			handler.AppendFormatted("entity");
			handler.AppendLiteral(".SetValue(");
			handler.AppendFormatted(value);
			handler.AppendLiteral(", value);");
			stringBuilder10.AppendLine(ref handler);
			sb.AppendLine();
			stringBuilder = sb;
			StringBuilder stringBuilder11 = stringBuilder;
			handler = new StringBuilder.AppendInterpolatedStringHandler(10, 1, stringBuilder);
			handler.AppendFormatted(indent);
			handler.AppendLiteral("#endregion");
			stringBuilder11.AppendLine(ref handler);
		}
	}

	private void AppendBehaviourExtensions(StringBuilder sb, string indent)
	{
		sb.AppendLine();
		sb.AppendLine();
		StringBuilder stringBuilder = sb;
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(23, 1, stringBuilder);
		handler.AppendFormatted(indent);
		handler.AppendLiteral("///Behaviour Extensions");
		stringBuilder2.AppendLine(ref handler);
		foreach (BehaviourDefinition linkedBehaviour in _definition.LinkedBehaviours)
		{
			string className = linkedBehaviour.ClassName;
			sb.AppendLine();
			stringBuilder = sb;
			StringBuilder stringBuilder3 = stringBuilder;
			handler = new StringBuilder.AppendInterpolatedStringHandler(8, 2, stringBuilder);
			handler.AppendFormatted(indent);
			handler.AppendLiteral("#region ");
			handler.AppendFormatted(className);
			stringBuilder3.AppendLine(ref handler);
			sb.AppendLine();
			AppendInliningAttribute(sb, indent);
			stringBuilder = sb;
			StringBuilder stringBuilder4 = stringBuilder;
			handler = new StringBuilder.AppendInterpolatedStringHandler(52, 6, stringBuilder);
			handler.AppendFormatted(indent);
			handler.AppendLiteral("public static bool Has");
			handler.AppendFormatted(className);
			handler.AppendLiteral("(this ");
			handler.AppendFormatted(_definition.EntityType);
			handler.AppendLiteral(" ");
			handler.AppendFormatted("entity");
			handler.AppendLiteral(") => ");
			handler.AppendFormatted("entity");
			handler.AppendLiteral(".HasBehaviour<");
			handler.AppendFormatted(className);
			handler.AppendLiteral(">();");
			stringBuilder4.AppendLine(ref handler);
			sb.AppendLine();
			AppendInliningAttribute(sb, indent);
			stringBuilder = sb;
			StringBuilder stringBuilder5 = stringBuilder;
			handler = new StringBuilder.AppendInterpolatedStringHandler(48, 7, stringBuilder);
			handler.AppendFormatted(indent);
			handler.AppendLiteral("public static ");
			handler.AppendFormatted(className);
			handler.AppendLiteral(" Get");
			handler.AppendFormatted(className);
			handler.AppendLiteral("(this ");
			handler.AppendFormatted(_definition.EntityType);
			handler.AppendLiteral(" ");
			handler.AppendFormatted("entity");
			handler.AppendLiteral(") => ");
			handler.AppendFormatted("entity");
			handler.AppendLiteral(".GetBehaviour<");
			handler.AppendFormatted(className);
			handler.AppendLiteral(">();");
			stringBuilder5.AppendLine(ref handler);
			sb.AppendLine();
			AppendInliningAttribute(sb, indent);
			stringBuilder = sb;
			StringBuilder stringBuilder6 = stringBuilder;
			handler = new StringBuilder.AppendInterpolatedStringHandler(85, 6, stringBuilder);
			handler.AppendFormatted(indent);
			handler.AppendLiteral("public static bool TryGet");
			handler.AppendFormatted(className);
			handler.AppendLiteral("(this ");
			handler.AppendFormatted(_definition.EntityType);
			handler.AppendLiteral(" ");
			handler.AppendFormatted("entity");
			handler.AppendLiteral(", out ");
			handler.AppendFormatted(className);
			handler.AppendLiteral(" behaviour) => ");
			handler.AppendFormatted("entity");
			handler.AppendLiteral(".TryGetBehaviour(out behaviour);");
			stringBuilder6.AppendLine(ref handler);
			sb.AppendLine();
			AppendInliningAttribute(sb, indent);
			if (linkedBehaviour.ConstructorParameters.Count == 0)
			{
				stringBuilder = sb;
				StringBuilder stringBuilder7 = stringBuilder;
				handler = new StringBuilder.AppendInterpolatedStringHandler(56, 6, stringBuilder);
				handler.AppendFormatted(indent);
				handler.AppendLiteral("public static void Add");
				handler.AppendFormatted(className);
				handler.AppendLiteral("(this ");
				handler.AppendFormatted(_definition.EntityType);
				handler.AppendLiteral(" ");
				handler.AppendFormatted("entity");
				handler.AppendLiteral(") => ");
				handler.AppendFormatted("entity");
				handler.AppendLiteral(".AddBehaviour(new ");
				handler.AppendFormatted(className);
				handler.AppendLiteral("());");
				stringBuilder7.AppendLine(ref handler);
			}
			else
			{
				string value = string.Join(", ", linkedBehaviour.ConstructorParameters.Select<(string, string), string>(((string Name, string Type) p) => p.Type + " " + p.Name));
				string value2 = string.Join(", ", linkedBehaviour.ConstructorParameters.Select<(string, string), string>(((string Name, string Type) p) => p.Name));
				stringBuilder = sb;
				StringBuilder stringBuilder8 = stringBuilder;
				handler = new StringBuilder.AppendInterpolatedStringHandler(58, 8, stringBuilder);
				handler.AppendFormatted(indent);
				handler.AppendLiteral("public static void Add");
				handler.AppendFormatted(className);
				handler.AppendLiteral("(this ");
				handler.AppendFormatted(_definition.EntityType);
				handler.AppendLiteral(" ");
				handler.AppendFormatted("entity");
				handler.AppendLiteral(", ");
				handler.AppendFormatted(value);
				handler.AppendLiteral(") => ");
				handler.AppendFormatted("entity");
				handler.AppendLiteral(".AddBehaviour(new ");
				handler.AppendFormatted(className);
				handler.AppendLiteral("(");
				handler.AppendFormatted(value2);
				handler.AppendLiteral("));");
				stringBuilder8.AppendLine(ref handler);
			}
			sb.AppendLine();
			AppendInliningAttribute(sb, indent);
			stringBuilder = sb;
			StringBuilder stringBuilder9 = stringBuilder;
			handler = new StringBuilder.AppendInterpolatedStringHandler(52, 6, stringBuilder);
			handler.AppendFormatted(indent);
			handler.AppendLiteral("public static bool Del");
			handler.AppendFormatted(className);
			handler.AppendLiteral("(this ");
			handler.AppendFormatted(_definition.EntityType);
			handler.AppendLiteral(" ");
			handler.AppendFormatted("entity");
			handler.AppendLiteral(") => ");
			handler.AppendFormatted("entity");
			handler.AppendLiteral(".DelBehaviour<");
			handler.AppendFormatted(className);
			handler.AppendLiteral(">();");
			stringBuilder9.AppendLine(ref handler);
			sb.AppendLine();
			stringBuilder = sb;
			StringBuilder stringBuilder10 = stringBuilder;
			handler = new StringBuilder.AppendInterpolatedStringHandler(10, 1, stringBuilder);
			handler.AppendFormatted(indent);
			handler.AppendLiteral("#endregion");
			stringBuilder10.AppendLine(ref handler);
		}
	}

	private void AppendInliningAttribute(StringBuilder sb, string indent)
	{
		if (_definition.AggressiveInlining)
		{
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(0, 2, sb);
			handler.AppendFormatted(indent);
			handler.AppendFormatted("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
			sb.AppendLine(ref handler);
		}
	}

	private static bool IsObjectType(string type)
	{
		bool flag = string.IsNullOrEmpty(type);
		if (!flag)
		{
			bool flag2 = ((type == "object" || type == "Object") ? true : false);
			flag = flag2;
		}
		return flag;
	}

	private async Task<bool> IsOurGeneratedFileAsync(string filePath)
	{
		try
		{
			string text = string.Join("\n", (await File.ReadAllLinesAsync(filePath)).Take(20));
			if (_config.TrackOrphans && text.Contains("AtomicGenerator: track file", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
			if (text.Contains("Source file path:", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
			if (text.Contains("Code generation. Don't modify!", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
			return false;
		}
		catch
		{
			return false;
		}
	}
}
