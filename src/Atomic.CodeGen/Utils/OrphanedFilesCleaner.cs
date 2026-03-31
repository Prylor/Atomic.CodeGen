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
		List<string> list = FileScanner.FindFiles(projectRoot, "**/*.cs", excludePatterns);
		foreach (string file in list)
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
		_ = 1;
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
			string text = match.Groups[1].Value.Trim();
			if (!Path.IsPathRooted(text))
			{
				text = Path.Combine(projectRoot, text);
			}
			if (!File.Exists(text))
			{
				return true;
			}
			string text2 = await File.ReadAllTextAsync(text);
			if (string.IsNullOrWhiteSpace(text2))
			{
				return true;
			}
			bool num = text2.Contains("[EntityAPI") || text2.Contains("EntityAPIAttribute");
			bool flag = text2.Contains("IEntityDomain") || text2.Contains("EntityDomainBuilder");
			if (!num && !flag)
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
			string text = generatedFilePath + ".meta";
			if (File.Exists(text))
			{
				File.Delete(text);
				Logger.LogVerbose("Deleted: " + text);
			}
		}
		catch (Exception ex)
		{
			Logger.LogError("Failed to cleanup " + generatedFilePath + ": " + ex.Message);
		}
	}
}
