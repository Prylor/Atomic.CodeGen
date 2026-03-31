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
		StringBuilder sb = new StringBuilder();
		bool hasNamespace = !string.IsNullOrWhiteSpace(_definition.Namespace);
		string text = (hasNamespace ? _indent : "");
		string indent = text + _indent;
		AppendHeader(sb);
		AppendUsings(sb);
		sb.AppendLine();
		if (hasNamespace)
		{
			sb.AppendLine($"namespace {_definition.Namespace}");
			sb.AppendLine("{");
		}
		sb.AppendLine("#if UNITY_EDITOR");
		sb.AppendLine($"{text}[InitializeOnLoad]");
		sb.AppendLine("#endif");
		sb.AppendLine($"{text}public static partial class {_definition.ClassName}");
		sb.AppendLine($"{text}{{");
		if (_definition.Tags.Count > 0)
		{
			AppendTagFields(sb, indent);
		}
		if (_definition.Values.Count > 0)
		{
			AppendValueFields(sb, indent);
		}
		AppendStaticConstructor(sb, indent);
		if (_definition.Tags.Count > 0)
		{
			AppendTagExtensions(sb, indent);
		}
		if (_definition.Values.Count > 0)
		{
			AppendValueExtensions(sb, indent);
		}
		if (_definition.LinkedBehaviours.Count > 0)
		{
			AppendBehaviourExtensions(sb, indent);
		}
		sb.AppendLine($"{text}}}");
		if (hasNamespace)
		{
			sb.AppendLine("}");
		}
		return sb.ToString();
	}

	private void AppendHeader(StringBuilder sb)
	{
		sb.AppendLine("/**");
		sb.AppendLine(" * Code generation. Don't modify!");
		sb.AppendLine($" * Generated from: {Path.GetFileName(_definition.SourceFile)}");
		sb.AppendLine($" * Source file path: {_definition.SourceFile}");
		if (_config.IncludeTimestamp)
		{
			sb.AppendLine($" * Generated at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
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
				sb.AppendLine($"using {import};");
			}
		}
		foreach (string item in from ns in (from b in _definition.LinkedBehaviours
				where !string.IsNullOrWhiteSpace(b.Namespace)
				select b.Namespace).Distinct()
			where ns != _definition.Namespace && !_definition.Imports.Contains(ns)
			select ns)
		{
			sb.AppendLine($"using {item};");
		}
		foreach (string item2 in from imp in _definition.LinkedBehaviours.SelectMany((BehaviourDefinition b) => b.RequiredImports).Distinct()
			where imp != _definition.Namespace && !_definition.Imports.Contains(imp)
			select imp)
		{
			if (!string.IsNullOrWhiteSpace(item2))
			{
				sb.AppendLine($"using {item2};");
			}
		}
	}

	private void AppendTagFields(StringBuilder sb, string indent)
	{
		sb.AppendLine();
		sb.AppendLine($"{indent}///Tags");
		foreach (string tag in _definition.Tags)
		{
			sb.AppendLine($"{indent}public static readonly int {tag};");
		}
	}

	private void AppendValueFields(StringBuilder sb, string indent)
	{
		sb.AppendLine();
		sb.AppendLine($"{indent}///Values");
		foreach (KeyValuePair<string, string> value4 in _definition.Values)
		{
			value4.Deconstruct(out var key, out var value);
			string value2 = key;
			string text = value;
			string value3 = (IsObjectType(text) ? "" : (" // " + text));
			sb.AppendLine($"{indent}public static readonly int {value2};{value3}");
		}
	}

	private void AppendStaticConstructor(StringBuilder sb, string indent)
	{
		string value = indent + _indent;
		sb.AppendLine();
		sb.AppendLine($"{indent}static {_definition.ClassName}()");
		sb.AppendLine($"{indent}{{");
		if (_definition.Tags.Count > 0)
		{
			sb.AppendLine($"{value}//Tags");
			foreach (string tag in _definition.Tags)
			{
				sb.AppendLine($"{value}{tag} = NameToId(nameof({tag}));");
			}
		}
		if (_definition.Values.Count > 0)
		{
			if (_definition.Tags.Count > 0)
			{
				sb.AppendLine();
			}
			sb.AppendLine($"{value}//Values");
			foreach (KeyValuePair<string, string> value4 in _definition.Values)
			{
				value4.Deconstruct(out var key, out var _);
				string value3 = key;
				sb.AppendLine($"{value}{value3} = NameToId(nameof({value3}));");
			}
		}
		sb.AppendLine($"{indent}}}");
	}

	private void AppendTagExtensions(StringBuilder sb, string indent)
	{
		sb.AppendLine();
		sb.AppendLine();
		sb.AppendLine($"{indent}///Tag Extensions");
		foreach (string tag in _definition.Tags)
		{
			sb.AppendLine();
			sb.AppendLine($"{indent}#region {tag}");
			sb.AppendLine();
			AppendInliningAttribute(sb, indent);
			sb.AppendLine($"{indent}public static bool Has{tag}Tag(this {_definition.EntityType} entity) => entity.HasTag({tag});");
			sb.AppendLine();
			AppendInliningAttribute(sb, indent);
			sb.AppendLine($"{indent}public static bool Add{tag}Tag(this {_definition.EntityType} entity) => entity.AddTag({tag});");
			sb.AppendLine();
			AppendInliningAttribute(sb, indent);
			sb.AppendLine($"{indent}public static bool Del{tag}Tag(this {_definition.EntityType} entity) => entity.DelTag({tag});");
			sb.AppendLine();
			sb.AppendLine($"{indent}#endregion");
		}
	}

	private void AppendValueExtensions(StringBuilder sb, string indent)
	{
		sb.AppendLine();
		sb.AppendLine();
		sb.AppendLine($"{indent}///Value Extensions");
		foreach (var (value, value2) in _definition.Values)
		{
			sb.AppendLine();
			sb.AppendLine($"{indent}#region {value}");
			sb.AppendLine();
			string value3 = (_definition.UnsafeAccess ? "Unsafe" : "");
			string value4 = (_definition.UnsafeAccess ? "ref " : "");
			AppendInliningAttribute(sb, indent);
			sb.AppendLine($"{indent}public static {value2} Get{value}(this {_definition.EntityType} entity) => entity.GetValue{value3}<{value2}>({value});");
			if (_definition.UnsafeAccess)
			{
				sb.AppendLine();
				sb.AppendLine($"{indent}public static {value4}{value2} Ref{value}(this {_definition.EntityType} entity) => {value4}entity.GetValue{value3}<{value2}>({value});");
			}
			sb.AppendLine();
			AppendInliningAttribute(sb, indent);
			sb.AppendLine($"{indent}public static bool TryGet{value}(this {_definition.EntityType} entity, out {value2} value) => entity.TryGetValue{value3}({value}, out value);");
			sb.AppendLine();
			AppendInliningAttribute(sb, indent);
			sb.AppendLine($"{indent}public static void Add{value}(this {_definition.EntityType} entity, {value2} value) => entity.AddValue({value}, value);");
			sb.AppendLine();
			AppendInliningAttribute(sb, indent);
			sb.AppendLine($"{indent}public static bool Has{value}(this {_definition.EntityType} entity) => entity.HasValue({value});");
			sb.AppendLine();
			AppendInliningAttribute(sb, indent);
			sb.AppendLine($"{indent}public static bool Del{value}(this {_definition.EntityType} entity) => entity.DelValue({value});");
			sb.AppendLine();
			AppendInliningAttribute(sb, indent);
			sb.AppendLine($"{indent}public static void Set{value}(this {_definition.EntityType} entity, {value2} value) => entity.SetValue({value}, value);");
			sb.AppendLine();
			sb.AppendLine($"{indent}#endregion");
		}
	}

	private void AppendBehaviourExtensions(StringBuilder sb, string indent)
	{
		sb.AppendLine();
		sb.AppendLine();
		sb.AppendLine($"{indent}///Behaviour Extensions");
		foreach (BehaviourDefinition linkedBehaviour in _definition.LinkedBehaviours)
		{
			string className = linkedBehaviour.ClassName;
			sb.AppendLine();
			sb.AppendLine($"{indent}#region {className}");
			sb.AppendLine();
			AppendInliningAttribute(sb, indent);
			sb.AppendLine($"{indent}public static bool Has{className}(this {_definition.EntityType} entity) => entity.HasBehaviour<{className}>();");
			sb.AppendLine();
			AppendInliningAttribute(sb, indent);
			sb.AppendLine($"{indent}public static {className} Get{className}(this {_definition.EntityType} entity) => entity.GetBehaviour<{className}>();");
			sb.AppendLine();
			AppendInliningAttribute(sb, indent);
			sb.AppendLine($"{indent}public static bool TryGet{className}(this {_definition.EntityType} entity, out {className} behaviour) => entity.TryGetBehaviour(out behaviour);");
			sb.AppendLine();
			AppendInliningAttribute(sb, indent);
			if (linkedBehaviour.ConstructorParameters.Count == 0)
			{
				sb.AppendLine($"{indent}public static void Add{className}(this {_definition.EntityType} entity) => entity.AddBehaviour(new {className}());");
			}
			else
			{
				string value = string.Join(", ", linkedBehaviour.ConstructorParameters.Select<(string, string), string>(((string Name, string Type) p) => p.Type + " " + p.Name));
				string value2 = string.Join(", ", linkedBehaviour.ConstructorParameters.Select<(string, string), string>(((string Name, string Type) p) => p.Name));
				sb.AppendLine($"{indent}public static void Add{className}(this {_definition.EntityType} entity, {value}) => entity.AddBehaviour(new {className}({value2}));");
			}
			sb.AppendLine();
			AppendInliningAttribute(sb, indent);
			sb.AppendLine($"{indent}public static bool Del{className}(this {_definition.EntityType} entity) => entity.DelBehaviour<{className}>();");
			sb.AppendLine();
			sb.AppendLine($"{indent}#endregion");
		}
	}

	private void AppendInliningAttribute(StringBuilder sb, string indent)
	{
		if (_definition.AggressiveInlining)
		{
			sb.AppendLine($"{indent}{"[MethodImpl(MethodImplOptions.AggressiveInlining)]"}");
		}
	}

	private static bool IsObjectType(string type)
	{
		if (string.IsNullOrEmpty(type))
		{
			return true;
		}
		return type == "object" || type == "Object";
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
