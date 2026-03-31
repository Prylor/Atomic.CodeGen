using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Atomic.CodeGen.Rename.Models;
using Atomic.CodeGen.Utils;

namespace Atomic.CodeGen.Rename;

public sealed class RenameExecutor
{
	private readonly string _projectRoot;

	private string? _backupDirectory;

	private readonly Dictionary<string, string> _backupPaths = new Dictionary<string, string>();

	public RenameExecutor(string projectRoot)
	{
		_projectRoot = projectRoot;
	}

	public bool Execute(RenameContext context, bool dryRun = false, int backupCap = 10)
	{
		if (!context.IsValid)
		{
			Logger.LogError("Cannot execute rename: context has validation errors");
			return false;
		}
		if (dryRun)
		{
			Logger.LogInfo("Dry run mode - no changes will be made");
			return true;
		}
		try
		{
			CreateBackup(context);
			if (backupCap > 0)
			{
				CleanupOldBackups(backupCap);
			}
			Dictionary<string, List<RenameOperation>> dictionary = (from u in context.CertainUsages
				select RenameOperation.FromUsageMatch(u, File.ReadAllText(u.FilePath)) into op
				group op by op.FilePath).ToDictionary((IGrouping<string, RenameOperation> g) => g.Key, (IGrouping<string, RenameOperation> g) => g.OrderByDescending((RenameOperation op) => op.StartOffset).ToList());
			foreach (var (filePath, operations) in dictionary)
			{
				ApplyOperationsToFile(filePath, operations);
			}
			if (context.RenameSourceFile && !string.IsNullOrEmpty(context.SourceFilePath))
			{
				RenameSourceFile(context);
			}
			if (context.FileRenames.Count > 0)
			{
				RenameGeneratedFiles(context);
			}
			Logger.LogInfo("Successfully renamed " + context.OldName + " to " + context.NewName);
			Logger.LogInfo($"Modified {dictionary.Count} file(s)");
			Logger.LogInfo("Backup saved to: " + _backupDirectory);
			return true;
		}
		catch (Exception ex)
		{
			Logger.LogError("Rename failed: " + ex.Message);
			Logger.LogInfo("Rolling back changes...");
			try
			{
				Rollback();
				Logger.LogInfo("Rollback successful");
			}
			catch (Exception ex2)
			{
				Logger.LogError("Rollback failed: " + ex2.Message);
				Logger.LogError("Manual restore from backup: " + _backupDirectory);
			}
			return false;
		}
	}

	private void CreateBackup(RenameContext context)
	{
		_backupDirectory = Path.Combine(_projectRoot, ".rename-backup", $"{context.Type}_{context.OldName}_{DateTime.Now:yyyyMMdd_HHmmss}");
		Directory.CreateDirectory(_backupDirectory);
		foreach (string affectedFile in context.AffectedFiles)
		{
			if (File.Exists(affectedFile))
			{
				string relativePath = GetRelativePath(affectedFile);
				string text = Path.Combine(_backupDirectory, relativePath);
				string directoryName = Path.GetDirectoryName(text);
				if (!string.IsNullOrEmpty(directoryName))
				{
					Directory.CreateDirectory(directoryName);
				}
				File.Copy(affectedFile, text, overwrite: true);
				_backupPaths[affectedFile] = text;
			}
		}
		Logger.LogVerbose($"Created backup of {_backupPaths.Count} files");
	}

	private void CleanupOldBackups(int cap)
	{
		string path = Path.Combine(_projectRoot, ".rename-backup");
		if (!Directory.Exists(path))
		{
			return;
		}
		List<DirectoryInfo> list = (from d in Directory.GetDirectories(path)
			select new DirectoryInfo(d) into d
			orderby d.CreationTime descending
			select d).ToList();
		if (list.Count <= cap)
		{
			return;
		}
		List<DirectoryInfo> list2 = list.Skip(cap).ToList();
		foreach (DirectoryInfo item in list2)
		{
			try
			{
				item.Delete(recursive: true);
				Logger.LogVerbose("Deleted old backup: " + item.Name);
			}
			catch (Exception ex)
			{
				Logger.LogWarning("Failed to delete old backup " + item.Name + ": " + ex.Message);
			}
		}
		if (list2.Count > 0)
		{
			Logger.LogInfo($"Cleaned up {list2.Count} old backup(s), keeping {cap} most recent");
		}
	}

	private void ApplyOperationsToFile(string filePath, List<RenameOperation> operations)
	{
		string text = File.ReadAllText(filePath);
		foreach (RenameOperation operation in operations)
		{
			if (operation.StartOffset < 0 || operation.StartOffset + operation.Length > text.Length)
			{
				Logger.LogWarning($"Invalid offset in {filePath} at {operation.Line}:{operation.Column}, skipping");
			}
			else
			{
				text = text.Substring(0, operation.StartOffset) + operation.NewText + text.Substring(operation.StartOffset + operation.Length);
			}
		}
		File.WriteAllText(filePath, text);
		Logger.LogVerbose($"Modified: {GetRelativePath(filePath)} ({operations.Count} changes)");
	}

	private void RenameSourceFile(RenameContext context)
	{
		string sourceFilePath = context.SourceFilePath;
		string text = Path.Combine(Path.GetDirectoryName(sourceFilePath) ?? string.Empty, context.NewName + Path.GetExtension(sourceFilePath));
		if (File.Exists(text))
		{
			throw new InvalidOperationException("Cannot rename file: " + text + " already exists");
		}
		File.Move(sourceFilePath, text);
		Logger.LogVerbose("Renamed file: " + GetRelativePath(sourceFilePath) + " -> " + Path.GetFileName(text));
		string text2 = sourceFilePath + ".meta";
		string text3 = text + ".meta";
		if (File.Exists(text2))
		{
			File.Move(text2, text3);
			Logger.LogVerbose("Renamed meta file: " + Path.GetFileName(text2) + " -> " + Path.GetFileName(text3));
		}
		UpdateCsprojReferences(sourceFilePath, text);
	}

	private void UpdateCsprojReferences(string oldPath, string newPath)
	{
		string[] files = Directory.GetFiles(_projectRoot, "*.csproj", SearchOption.AllDirectories);
		string text = GetRelativePath(oldPath).Replace('/', '\\');
		string text2 = GetRelativePath(newPath).Replace('/', '\\');
		string[] array = files;
		foreach (string path in array)
		{
			string text3 = File.ReadAllText(path);
			if (text3.Contains(text) || text3.Contains(text.Replace('\\', '/')))
			{
				string text4 = text3.Replace(text, text2).Replace(text.Replace('\\', '/'), text2.Replace('\\', '/'));
				if (text4 != text3)
				{
					File.WriteAllText(path, text4);
					Logger.LogVerbose("Updated csproj: " + Path.GetFileName(path));
				}
			}
		}
	}

	private void RenameGeneratedFiles(RenameContext context)
	{
		foreach (var (text, text2) in context.FileRenames)
		{
			if (!File.Exists(text))
			{
				Logger.LogWarning("File not found for rename: " + GetRelativePath(text));
				continue;
			}
			if (File.Exists(text2))
			{
				Logger.LogWarning("Cannot rename file, target already exists: " + GetRelativePath(text2));
				continue;
			}
			File.Move(text, text2);
			Logger.LogVerbose("Renamed file: " + Path.GetFileName(text) + " -> " + Path.GetFileName(text2));
			string text3 = text + ".meta";
			string text4 = text2 + ".meta";
			if (File.Exists(text3))
			{
				File.Move(text3, text4);
				Logger.LogVerbose("Renamed meta file: " + Path.GetFileName(text3) + " -> " + Path.GetFileName(text4));
			}
			UpdateCsprojReferences(text, text2);
		}
		Logger.LogInfo($"Renamed {context.FileRenames.Count} generated file(s)");
	}

	private void Rollback()
	{
		foreach (var (destFileName, text3) in _backupPaths)
		{
			if (File.Exists(text3))
			{
				File.Copy(text3, destFileName, overwrite: true);
			}
		}
	}

	public void DeleteBackup()
	{
		if (!string.IsNullOrEmpty(_backupDirectory) && Directory.Exists(_backupDirectory))
		{
			Directory.Delete(_backupDirectory, recursive: true);
			Logger.LogVerbose("Backup deleted");
		}
	}

	private string GetRelativePath(string fullPath)
	{
		if (fullPath.StartsWith(_projectRoot, StringComparison.OrdinalIgnoreCase))
		{
			return fullPath.Substring(_projectRoot.Length).TrimStart(Path.DirectorySeparatorChar, '/');
		}
		return fullPath;
	}

	public static List<FileChangeSummary> GetPreview(RenameContext context)
	{
		List<FileChangeSummary> list = new List<FileChangeSummary>();
		foreach (var (filePath, list3) in context.UsagesByFile)
		{
			list.Add(new FileChangeSummary
			{
				FilePath = filePath,
				ChangeCount = list3.Count,
				AmbiguousCount = list3.Count((UsageMatch u) => u.IsAmbiguous),
				Categories = list3.Select((UsageMatch u) => u.Category).Distinct().ToList(),
				Usages = list3
			});
		}
		return list.OrderBy((FileChangeSummary s) => s.FilePath).ToList();
	}
}
