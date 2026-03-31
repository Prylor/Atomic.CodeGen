using System.Collections.Generic;
using System.IO;
using System.Linq;
using Atomic.CodeGen.Core.Models;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace Atomic.CodeGen.Utils;

public sealed class FileScanner
{
	private readonly CodeGenConfig _config;

	public FileScanner(CodeGenConfig config)
	{
		_config = config;
	}

	public List<string> Scan()
	{
		return FindFiles(_config.GetAbsoluteProjectRoot(), _config.ScanPaths, _config.ExcludePaths);
	}

	public static List<string> FindFiles(string projectRoot, string pattern, List<string> excludePatterns)
	{
		return FindFiles(projectRoot, new List<string> { pattern }, excludePatterns);
	}

	public static List<string> FindFiles(string projectRoot, List<string> includePatterns, List<string> excludePatterns)
	{
		if (!Directory.Exists(projectRoot))
		{
			Logger.LogVerbose("Directory does not exist: " + projectRoot);
			return new List<string>();
		}
		Matcher matcher = new Matcher();
		foreach (string includePattern in includePatterns)
		{
			matcher.AddInclude(includePattern);
			Logger.LogVerbose("Include pattern: " + includePattern);
		}
		foreach (string excludePattern in excludePatterns)
		{
			matcher.AddExclude(excludePattern);
			Logger.LogVerbose("Exclude pattern: " + excludePattern);
		}
		DirectoryInfoWrapper directoryInfo = new DirectoryInfoWrapper(new DirectoryInfo(projectRoot));
		List<string> list = matcher.Execute(directoryInfo).Files.Select((FilePatternMatch f) => Path.Combine(projectRoot, f.Path)).Where(File.Exists).ToList();
		Logger.LogVerbose($"Found {list.Count} files matching patterns");
		return list;
	}
}
