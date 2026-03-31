using System;
using System.IO;

namespace Atomic.CodeGen.Utils;

public static class GitIgnoreHelper
{
	private const string RenameBackupEntry = ".rename-backup/";

	public static bool EnsureRenameBackupIgnored(string projectRoot)
	{
		string path = Path.Combine(projectRoot, ".gitignore");
		if (!File.Exists(path))
		{
			Logger.LogVerbose("No .gitignore found, skipping");
			return false;
		}
		string gitignoreContent = File.ReadAllText(path);
		string[] lines = gitignoreContent.Split(new string[2] { "\r\n", "\n" }, StringSplitOptions.None);
		for (int i = 0; i < lines.Length; i++)
		{
			string trimmedLine = lines[i].Trim();
			if (trimmedLine == ".rename-backup" || trimmedLine == ".rename-backup/")
			{
				Logger.LogVerbose(".rename-backup already in .gitignore");
				return false;
			}
		}
		string trailingNewline = (gitignoreContent.EndsWith("\n") ? "" : Environment.NewLine);
		string contents = gitignoreContent + trailingNewline + ".rename-backup/" + Environment.NewLine;
		File.WriteAllText(path, contents);
		Logger.LogInfo("Added .rename-backup/ to .gitignore");
		return true;
	}

	public static bool GitIgnoreExists(string projectRoot)
	{
		return File.Exists(Path.Combine(projectRoot, ".gitignore"));
	}
}
