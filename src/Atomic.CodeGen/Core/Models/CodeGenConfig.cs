using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;

namespace Atomic.CodeGen.Core.Models;

public sealed class CodeGenConfig
{
	[JsonPropertyName("version")]
	public string Version { get; set; } = "1.0";

	[JsonPropertyName("projectRoot")]
	public string ProjectRoot { get; set; } = ".";

	[JsonPropertyName("analyzerMode")]
	public AnalyzerMode AnalyzerMode { get; set; }

	[JsonPropertyName("includedProjects")]
	public List<string>? IncludedProjects { get; set; }

	[JsonPropertyName("scanPaths")]
	public List<string> ScanPaths { get; set; } = new List<string> { "Assets/**/*EntityAPI*.cs", "Packages/**/*EntityAPI*.cs" };

	[JsonPropertyName("excludePaths")]
	public List<string> ExcludePaths { get; set; } = new List<string> { "**/obj/**", "**/Library/**", "**/Temp/**", "**/*.g.cs", "**/*.generated.cs" };

	[JsonPropertyName("verbose")]
	public bool Verbose { get; set; }

	[JsonPropertyName("trackOrphans")]
	public bool TrackOrphans { get; set; } = true;

	[JsonPropertyName("formatting")]
	public FormattingOptions Formatting { get; set; } = new FormattingOptions();

	[JsonPropertyName("backupCap")]
	public int BackupCap { get; set; } = 10;

	public string GetAbsoluteProjectRoot()
	{
		if (!Path.IsPathRooted(ProjectRoot))
		{
			return Path.GetFullPath(ProjectRoot);
		}
		return ProjectRoot;
	}
}
