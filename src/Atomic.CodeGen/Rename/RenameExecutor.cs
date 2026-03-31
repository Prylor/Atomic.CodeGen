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
				string backupFilePath = Path.Combine(_backupDirectory, relativePath);
				string directoryName = Path.GetDirectoryName(backupFilePath);
				if (!string.IsNullOrEmpty(directoryName))
				{
					Directory.CreateDirectory(directoryName);
				}
				File.Copy(affectedFile, backupFilePath, overwrite: true);
				_backupPaths[affectedFile] = backupFilePath;
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
		List<DirectoryInfo> allBackups = (from d in Directory.GetDirectories(path)
			select new DirectoryInfo(d) into d
			orderby d.CreationTime descending
			select d).ToList();
		if (allBackups.Count <= cap)
		{
			return;
		}
		List<DirectoryInfo> backupsToDelete = allBackups.Skip(cap).ToList();
		foreach (DirectoryInfo backupDir in backupsToDelete)
		{
			try
			{
				backupDir.Delete(recursive: true);
				Logger.LogVerbose("Deleted old backup: " + backupDir.Name);
			}
			catch (Exception ex)
			{
				Logger.LogWarning("Failed to delete old backup " + backupDir.Name + ": " + ex.Message);
			}
		}
		if (backupsToDelete.Count > 0)
		{
			Logger.LogInfo($"Cleaned up {backupsToDelete.Count} old backup(s), keeping {cap} most recent");
		}
	}

	private void ApplyOperationsToFile(string filePath, List<RenameOperation> operations)
	{
		string fileContent = File.ReadAllText(filePath);
		foreach (RenameOperation operation in operations)
		{
			if (operation.StartOffset < 0 || operation.StartOffset + operation.Length > fileContent.Length)
			{
				Logger.LogWarning($"Invalid offset in {filePath} at {operation.Line}:{operation.Column}, skipping");
			}
			else
			{
				fileContent = fileContent.Substring(0, operation.StartOffset) + operation.NewText + fileContent.Substring(operation.StartOffset + operation.Length);
			}
		}
		File.WriteAllText(filePath, fileContent);
		Logger.LogVerbose($"Modified: {GetRelativePath(filePath)} ({operations.Count} changes)");
	}

	private void RenameSourceFile(RenameContext context)
	{
		string sourceFilePath = context.SourceFilePath;
		string newFilePath = Path.Combine(Path.GetDirectoryName(sourceFilePath) ?? string.Empty, context.NewName + Path.GetExtension(sourceFilePath));
		if (File.Exists(newFilePath))
		{
			throw new InvalidOperationException("Cannot rename file: " + newFilePath + " already exists");
		}
		File.Move(sourceFilePath, newFilePath);
		Logger.LogVerbose("Renamed file: " + GetRelativePath(sourceFilePath) + " -> " + Path.GetFileName(newFilePath));
		string oldMetaPath = sourceFilePath + ".meta";
		string newMetaPath = newFilePath + ".meta";
		if (File.Exists(oldMetaPath))
		{
			File.Move(oldMetaPath, newMetaPath);
			Logger.LogVerbose("Renamed meta file: " + Path.GetFileName(oldMetaPath) + " -> " + Path.GetFileName(newMetaPath));
		}
		UpdateCsprojReferences(sourceFilePath, newFilePath);
	}

	private void UpdateCsprojReferences(string oldPath, string newPath)
	{
		string[] files = Directory.GetFiles(_projectRoot, "*.csproj", SearchOption.AllDirectories);
		string oldRelativePath = GetRelativePath(oldPath).Replace('/', '\\');
		string newRelativePath = GetRelativePath(newPath).Replace('/', '\\');
		string[] array = files;
		foreach (string csprojPath in array)
		{
			string csprojContent = File.ReadAllText(csprojPath);
			if (csprojContent.Contains(oldRelativePath) || csprojContent.Contains(oldRelativePath.Replace('\\', '/')))
			{
				string updatedContent = csprojContent.Replace(oldRelativePath, newRelativePath).Replace(oldRelativePath.Replace('\\', '/'), newRelativePath.Replace('\\', '/'));
				if (updatedContent != csprojContent)
				{
					File.WriteAllText(csprojPath, updatedContent);
					Logger.LogVerbose("Updated csproj: " + Path.GetFileName(csprojPath));
				}
			}
		}
	}

	private void RenameGeneratedFiles(RenameContext context)
	{
		foreach (var (oldFilePath, newFilePath) in context.FileRenames)
		{
			if (!File.Exists(oldFilePath))
			{
				Logger.LogWarning("File not found for rename: " + GetRelativePath(oldFilePath));
				continue;
			}
			if (File.Exists(newFilePath))
			{
				Logger.LogWarning("Cannot rename file, target already exists: " + GetRelativePath(newFilePath));
				continue;
			}
			File.Move(oldFilePath, newFilePath);
			Logger.LogVerbose("Renamed file: " + Path.GetFileName(oldFilePath) + " -> " + Path.GetFileName(newFilePath));
			string oldMetaPath = oldFilePath + ".meta";
			string newMetaPath = newFilePath + ".meta";
			if (File.Exists(oldMetaPath))
			{
				File.Move(oldMetaPath, newMetaPath);
				Logger.LogVerbose("Renamed meta file: " + Path.GetFileName(oldMetaPath) + " -> " + Path.GetFileName(newMetaPath));
			}
			UpdateCsprojReferences(oldFilePath, newFilePath);
		}
		Logger.LogInfo($"Renamed {context.FileRenames.Count} generated file(s)");
	}

	private void Rollback()
	{
		foreach (var (originalPath, backupPath) in _backupPaths)
		{
			if (File.Exists(backupPath))
			{
				File.Copy(backupPath, originalPath, overwrite: true);
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
		List<FileChangeSummary> summaries = new List<FileChangeSummary>();
		foreach (var (filePath, fileUsages) in context.UsagesByFile)
		{
			summaries.Add(new FileChangeSummary
			{
				FilePath = filePath,
				ChangeCount = fileUsages.Count,
				AmbiguousCount = fileUsages.Count((UsageMatch u) => u.IsAmbiguous),
				Categories = fileUsages.Select((UsageMatch u) => u.Category).Distinct().ToList(),
				Usages = fileUsages
			});
		}
		return summaries.OrderBy((FileChangeSummary s) => s.FilePath).ToList();
	}
}
