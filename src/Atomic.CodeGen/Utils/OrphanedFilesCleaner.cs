using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Atomic.CodeGen.Core.Models;

namespace Atomic.CodeGen.Utils;

public static class OrphanedFilesCleaner
{
	private static readonly Regex SourceFilePathRegex = new Regex("^\\s*\\*\\s*Source file path:\\s*(.+?)\\s*$", RegexOptions.Multiline | RegexOptions.Compiled);

	private static readonly Regex TrackingFlagRegex = new Regex("^\\s*\\*\\s*AtomicGenerator:\\s*track\\s+file\\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

	public static async Task<int> CleanOrphanedFilesAsync(CodeGenConfig config, List<string> scanPaths, HashSet<string>? expectedGeneratedFiles = null)
	{
		int cleanedCount = 0;
		string projectRoot = config.GetAbsoluteProjectRoot();
		List<string> excludePatterns = new List<string> { "**/obj/**", "**/bin/**", "**/Library/**", "**/Temp/**" };
		List<string> csFiles = FileScanner.FindFiles(projectRoot, "**/*.cs", excludePatterns);
		foreach (string file in csFiles)
		{
			if (await IsOrphanedAsync(file, projectRoot, expectedGeneratedFiles))
			{
				await CleanupGeneratedFileAsync(file, projectRoot);
				cleanedCount++;
			}
		}
		return cleanedCount;
	}

	private static async Task<bool> IsOrphanedAsync(string generatedFilePath, string projectRoot, HashSet<string>? expectedGeneratedFiles)
	{
		try
		{
			if (!File.Exists(generatedFilePath))
			{
				return false;
			}
			string input = string.Join("\n", (await File.ReadAllLinesAsync(generatedFilePath)).Take(20));
			if (!TrackingFlagRegex.Match(input).Success)
			{
				return false;
			}
			Match match = SourceFilePathRegex.Match(input);
			if (!match.Success)
			{
				return false;
			}
			string fullPath = Path.GetFullPath(generatedFilePath);
			if (expectedGeneratedFiles != null && !expectedGeneratedFiles.Contains(fullPath))
			{
				return true;
			}
			string sourceFilePath = match.Groups[1].Value.Trim();
			if (!Path.IsPathRooted(sourceFilePath))
			{
				sourceFilePath = Path.Combine(projectRoot, sourceFilePath);
			}
			if (!File.Exists(sourceFilePath))
			{
				return true;
			}
			string sourceContent = await File.ReadAllTextAsync(sourceFilePath);
			if (string.IsNullOrWhiteSpace(sourceContent))
			{
				return true;
			}
			bool isEntityApi = sourceContent.Contains("[EntityAPI") || sourceContent.Contains("EntityAPIAttribute");
			bool isDomain = sourceContent.Contains("IEntityDomain") || sourceContent.Contains("EntityDomainBuilder");
			if (!isEntityApi && !isDomain)
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

	private static async Task CleanupGeneratedFileAsync(string generatedFilePath, string projectRoot)
	{
		try
		{
			Logger.LogWarning("Cleaning orphaned generated file: " + generatedFilePath);
			string[] files = Directory.GetFiles(projectRoot, "*.csproj", SearchOption.TopDirectoryOnly);
			string[] array = files;
			for (int i = 0; i < array.Length; i++)
			{
				await ProjectFileManager.RemoveFileFromProjectAsync(array[i], generatedFilePath, projectRoot);
			}
			if (File.Exists(generatedFilePath))
			{
				File.Delete(generatedFilePath);
				Logger.LogVerbose("Deleted: " + generatedFilePath);
			}
			string metaFilePath = generatedFilePath + ".meta";
			if (File.Exists(metaFilePath))
			{
				File.Delete(metaFilePath);
				Logger.LogVerbose("Deleted: " + metaFilePath);
			}
		}
		catch (Exception ex)
		{
			Logger.LogError("Failed to cleanup " + generatedFilePath + ": " + ex.Message);
		}
	}
}
