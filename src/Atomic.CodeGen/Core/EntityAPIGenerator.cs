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

	private const string RefModifier = "ref ";

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
		List<string> targetProjects = new List<string>();
		if (!string.IsNullOrWhiteSpace(_definition.TargetProject))
		{
			targetProjects.Add(_definition.TargetProject);
		}
		else
		{
			List<string> detectedProjects = ProjectDetector.FindProjectsForFile(_definition.SourceFile, _config.GetAbsoluteProjectRoot());
			if (detectedProjects.Count == 0)
			{
				Logger.LogWarning("Could not find any .csproj containing " + _definition.SourceFile + ". Generated file will not be linked to any project.");
			}
			else
			{
				targetProjects.AddRange(detectedProjects);
			}
		}
		foreach (string projectPath in targetProjects)
		{
			await ProjectFileManager.AddGeneratedFileAsync(Path.Combine(_config.GetAbsoluteProjectRoot(), projectPath), outputPath, _config.GetAbsoluteProjectRoot());
		}
		Logger.LogSuccess("Generated: " + outputPath);
		return true;
	}

	private async Task GenerateMetaFileAsync(string csFilePath)
	{
		string metaPath = csFilePath + ".meta";
		string guid = GenerateGuidFromPath(csFilePath);
		string contents = "fileFormatVersion: 2\r\nguid: " + guid + "\r\nMonoImporter:\r\n  externalObjects: {}\r\n  serializedVersion: 2\r\n  defaultReferences: []\r\n  executionOrder: 0\r\n  icon: {instanceID: 0}\r\n  userData:\r\n  assetBundleName:\r\n  assetBundleVariant:\r\n";
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
		string classIndent = (hasNamespace ? _indent : "");
		string memberIndent = classIndent + _indent;
		AppendHeader(sb);
		AppendUsings(sb);
		sb.AppendLine();
		if (hasNamespace)
		{
			sb.AppendLine($"namespace {_definition.Namespace}");
			sb.AppendLine("{");
		}
		sb.AppendLine("#if UNITY_EDITOR");
		sb.AppendLine($"{classIndent}[InitializeOnLoad]");
		sb.AppendLine("#endif");
		sb.AppendLine($"{classIndent}public static partial class {_definition.ClassName}");
		sb.AppendLine($"{classIndent}{{");
		if (_definition.Tags.Count > 0)
		{
			AppendTagFields(sb, memberIndent);
		}
		if (_definition.Values.Count > 0)
		{
			AppendValueFields(sb, memberIndent);
		}
		AppendStaticConstructor(sb, memberIndent);
		if (_definition.Tags.Count > 0)
		{
			AppendTagExtensions(sb, memberIndent);
		}
		if (_definition.Values.Count > 0)
		{
			AppendValueExtensions(sb, memberIndent);
		}
		if (_definition.LinkedBehaviours.Count > 0)
		{
			AppendBehaviourExtensions(sb, memberIndent);
		}
		sb.AppendLine($"{classIndent}}}");
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
		foreach (string behaviourNamespace in from ns in (from b in _definition.LinkedBehaviours
				where !string.IsNullOrWhiteSpace(b.Namespace)
				select b.Namespace).Distinct()
			where ns != _definition.Namespace && !_definition.Imports.Contains(ns)
			select ns)
		{
			sb.AppendLine($"using {behaviourNamespace};");
		}
		foreach (string requiredImport in from imp in _definition.LinkedBehaviours.SelectMany((BehaviourDefinition b) => b.RequiredImports).Distinct()
			where imp != _definition.Namespace && !_definition.Imports.Contains(imp)
			select imp)
		{
			if (!string.IsNullOrWhiteSpace(requiredImport))
			{
				sb.AppendLine($"using {requiredImport};");
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
		foreach (KeyValuePair<string, string> entry in _definition.Values)
		{
			entry.Deconstruct(out var key, out var value);
			string fieldName = key;
			string valueType = value;
			string typeComment = (IsObjectType(valueType) ? "" : (" // " + valueType));
			sb.AppendLine($"{indent}public static readonly int {fieldName};{typeComment}");
		}
	}

	private void AppendStaticConstructor(StringBuilder sb, string indent)
	{
		string bodyIndent = indent + _indent;
		sb.AppendLine();
		sb.AppendLine($"{indent}static {_definition.ClassName}()");
		sb.AppendLine($"{indent}{{");
		if (_definition.Tags.Count > 0)
		{
			sb.AppendLine($"{bodyIndent}//Tags");
			foreach (string tag in _definition.Tags)
			{
				sb.AppendLine($"{bodyIndent}{tag} = NameToId(nameof({tag}));");
			}
		}
		if (_definition.Values.Count > 0)
		{
			if (_definition.Tags.Count > 0)
			{
				sb.AppendLine();
			}
			sb.AppendLine($"{bodyIndent}//Values");
			foreach (KeyValuePair<string, string> entry in _definition.Values)
			{
				entry.Deconstruct(out var key, out var _);
				string fieldName = key;
				sb.AppendLine($"{bodyIndent}{fieldName} = NameToId(nameof({fieldName}));");
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
			sb.AppendLine($"{indent}public static bool Has{tag}Tag(this {_definition.EntityType} {ParamName}) => {ParamName}.HasTag({tag});");
			sb.AppendLine();
			AppendInliningAttribute(sb, indent);
			sb.AppendLine($"{indent}public static bool Add{tag}Tag(this {_definition.EntityType} {ParamName}) => {ParamName}.AddTag({tag});");
			sb.AppendLine();
			AppendInliningAttribute(sb, indent);
			sb.AppendLine($"{indent}public static bool Del{tag}Tag(this {_definition.EntityType} {ParamName}) => {ParamName}.DelTag({tag});");
			sb.AppendLine();
			sb.AppendLine($"{indent}#endregion");
		}
	}

	private void AppendValueExtensions(StringBuilder sb, string indent)
	{
		sb.AppendLine();
		sb.AppendLine();
		sb.AppendLine($"{indent}///Value Extensions");
		foreach (var (fieldName, valueType) in _definition.Values)
		{
			sb.AppendLine();
			sb.AppendLine($"{indent}#region {fieldName}");
			sb.AppendLine();
			string unsafeSuffix = (_definition.UnsafeAccess ? UnsafeSuffix : "");
			string refModifier = (_definition.UnsafeAccess ? RefModifier : "");
			AppendInliningAttribute(sb, indent);
			sb.AppendLine($"{indent}public static {valueType} Get{fieldName}(this {_definition.EntityType} {ParamName}) => {ParamName}.GetValue{unsafeSuffix}<{valueType}>({fieldName});");
			if (_definition.UnsafeAccess)
			{
				sb.AppendLine();
				sb.AppendLine($"{indent}public static {refModifier}{valueType} Ref{fieldName}(this {_definition.EntityType} {ParamName}) => {refModifier}{ParamName}.GetValue{unsafeSuffix}<{valueType}>({fieldName});");
			}
			sb.AppendLine();
			AppendInliningAttribute(sb, indent);
			sb.AppendLine($"{indent}public static bool TryGet{fieldName}(this {_definition.EntityType} entity, out {valueType} value) => {ParamName}.TryGetValue{unsafeSuffix}({fieldName}, out value);");
			sb.AppendLine();
			AppendInliningAttribute(sb, indent);
			sb.AppendLine($"{indent}public static void Add{fieldName}(this {_definition.EntityType} entity, {valueType} value) => {ParamName}.AddValue({fieldName}, value);");
			sb.AppendLine();
			AppendInliningAttribute(sb, indent);
			sb.AppendLine($"{indent}public static bool Has{fieldName}(this {_definition.EntityType} {ParamName}) => {ParamName}.HasValue({fieldName});");
			sb.AppendLine();
			AppendInliningAttribute(sb, indent);
			sb.AppendLine($"{indent}public static bool Del{fieldName}(this {_definition.EntityType} {ParamName}) => {ParamName}.DelValue({fieldName});");
			sb.AppendLine();
			AppendInliningAttribute(sb, indent);
			sb.AppendLine($"{indent}public static void Set{fieldName}(this {_definition.EntityType} entity, {valueType} value) => {ParamName}.SetValue({fieldName}, value);");
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
			sb.AppendLine($"{indent}public static bool Has{className}(this {_definition.EntityType} {ParamName}) => {ParamName}.HasBehaviour<{className}>();");
			sb.AppendLine();
			AppendInliningAttribute(sb, indent);
			sb.AppendLine($"{indent}public static {className} Get{className}(this {_definition.EntityType} {ParamName}) => {ParamName}.GetBehaviour<{className}>();");
			sb.AppendLine();
			AppendInliningAttribute(sb, indent);
			sb.AppendLine($"{indent}public static bool TryGet{className}(this {_definition.EntityType} entity, out {className} behaviour) => {ParamName}.TryGetBehaviour(out behaviour);");
			sb.AppendLine();
			AppendInliningAttribute(sb, indent);
			if (linkedBehaviour.ConstructorParameters.Count == 0)
			{
				sb.AppendLine($"{indent}public static void Add{className}(this {_definition.EntityType} {ParamName}) => {ParamName}.AddBehaviour(new {className}());");
			}
			else
			{
				string constructorParams = string.Join(", ", linkedBehaviour.ConstructorParameters.Select<(string, string), string>(((string Name, string Type) p) => p.Type + " " + p.Name));
				string constructorArgs = string.Join(", ", linkedBehaviour.ConstructorParameters.Select<(string, string), string>(((string Name, string Type) p) => p.Name));
				sb.AppendLine($"{indent}public static void Add{className}(this {_definition.EntityType} entity, {constructorParams}) => {ParamName}.AddBehaviour(new {className}({constructorArgs}));");
			}
			sb.AppendLine();
			AppendInliningAttribute(sb, indent);
			sb.AppendLine($"{indent}public static bool Del{className}(this {_definition.EntityType} {ParamName}) => {ParamName}.DelBehaviour<{className}>();");
			sb.AppendLine();
			sb.AppendLine($"{indent}#endregion");
		}
	}

	private void AppendInliningAttribute(StringBuilder sb, string indent)
	{
		if (_definition.AggressiveInlining)
		{
			sb.AppendLine($"{indent}{AggressiveInliningAttribute}");
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
			string headerContent = string.Join("\n", (await File.ReadAllLinesAsync(filePath)).Take(20));
			if (_config.TrackOrphans && headerContent.Contains("AtomicGenerator: track file", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
			if (headerContent.Contains("Source file path:", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
			if (headerContent.Contains("Code generation. Don't modify!", StringComparison.OrdinalIgnoreCase))
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
