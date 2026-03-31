using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Atomic.CodeGen.Core.Models;

namespace Atomic.CodeGen.Utils;

public static class ConfigLoader
{
	private const string DefaultConfigFileName = "atomic-codegen.json";

	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		WriteIndented = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		AllowTrailingCommas = true,
		ReadCommentHandling = JsonCommentHandling.Skip
	};

	public static async Task<CodeGenConfig> LoadAsync(string projectPath)
	{
		string configPath = Path.Combine(projectPath, "atomic-codegen.json");
		if (!File.Exists(configPath))
		{
			Logger.LogWarning("Configuration file not found: " + configPath);
			Logger.LogInfo("Using default configuration");
			return CreateDefault(projectPath);
		}
		try
		{
			CodeGenConfig codeGenConfig = JsonSerializer.Deserialize<CodeGenConfig>(await File.ReadAllTextAsync(configPath), JsonOptions);
			if (codeGenConfig == null)
			{
				Logger.LogWarning("Failed to deserialize configuration, using defaults");
				return CreateDefault(projectPath);
			}
			if (!Path.IsPathRooted(codeGenConfig.ProjectRoot))
			{
				codeGenConfig.ProjectRoot = Path.GetFullPath(Path.Combine(projectPath, codeGenConfig.ProjectRoot));
			}
			Logger.LogVerbose("Loaded configuration from: " + configPath);
			return codeGenConfig;
		}
		catch (Exception ex)
		{
			Logger.LogError("Failed to load configuration: " + ex.Message);
			Logger.LogInfo("Using default configuration");
			return CreateDefault(projectPath);
		}
	}

	public static async Task SaveAsync(CodeGenConfig config, string projectPath)
	{
		string configPath = Path.Combine(projectPath, "atomic-codegen.json");
		try
		{
			string contents = JsonSerializer.Serialize(config, JsonOptions);
			await File.WriteAllTextAsync(configPath, contents);
			Logger.LogInfo("Configuration saved to: " + configPath);
		}
		catch (Exception ex)
		{
			Logger.LogError("Failed to save configuration: " + ex.Message);
			throw;
		}
	}

	private static CodeGenConfig CreateDefault(string projectPath)
	{
		return new CodeGenConfig
		{
			ProjectRoot = projectPath,
			ScanPaths = new List<string> { "Assets/**/*EntityAPI*.cs", "Packages/**/*EntityAPI*.cs" },
			ExcludePaths = new List<string> { "**/obj/**", "**/Library/**", "**/Temp/**", "**/*.g.cs", "**/*.generated.cs" },
			Verbose = false
		};
	}
}
