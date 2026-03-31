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
		string text = File.ReadAllText(path);
		string[] array = text.Split(new string[2] { "\r\n", "\n" }, StringSplitOptions.None);
		for (int i = 0; i < array.Length; i++)
		{
			string text2 = array[i].Trim();
			if (text2 == ".rename-backup" || text2 == ".rename-backup/")
			{
				Logger.LogVerbose(".rename-backup already in .gitignore");
				return false;
			}
		}
		string text3 = (text.EndsWith("\n") ? "" : Environment.NewLine);
		string contents = text + text3 + ".rename-backup/" + Environment.NewLine;
		File.WriteAllText(path, contents);
		Logger.LogInfo("Added .rename-backup/ to .gitignore");
		return true;
	}

	public static bool GitIgnoreExists(string projectRoot)
	{
		return File.Exists(Path.Combine(projectRoot, ".gitignore"));
	}
}
