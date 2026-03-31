using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Atomic.CodeGen.Utils;

public static class ProjectDetector
{
	public static List<string> FindProjectsForFile(string sourceFilePath, string projectRoot)
	{
		List<string> matchingProjects = new List<string>();
		string[] csprojFiles = Directory.GetFiles(projectRoot, "*.csproj", SearchOption.TopDirectoryOnly);
		string[] array = csprojFiles;
		foreach (string csprojPath in array)
		{
			if (ContainsFileExplicitly(csprojPath, sourceFilePath, projectRoot))
			{
				matchingProjects.Add(Path.GetRelativePath(projectRoot, csprojPath));
			}
		}
		if (matchingProjects.Count > 0)
		{
			return matchingProjects;
		}
		array = csprojFiles;
		foreach (string csprojPath2 in array)
		{
			if (ContainsFileImplicitly(csprojPath2, sourceFilePath, projectRoot))
			{
				matchingProjects.Add(Path.GetRelativePath(projectRoot, csprojPath2));
			}
		}
		return matchingProjects;
	}

	private static bool ContainsFileExplicitly(string csprojPath, string sourceFilePath, string projectRoot)
	{
		try
		{
			XElement root = XDocument.Load(csprojPath).Root;
			if (root == null || root.Name.LocalName != "Project")
			{
				return false;
			}
			string path = Path.GetDirectoryName(csprojPath) ?? projectRoot;
			foreach (string compilePath in (from e in root.Descendants()
				where e.Name.LocalName == "Compile"
				select e.Attribute("Include")?.Value into includePath
				where includePath != null
				select includePath).ToList())
			{
				string fullPath = Path.GetFullPath(Path.Combine(path, compilePath));
				string fullPath2 = Path.GetFullPath(sourceFilePath);
				if (string.Equals(fullPath, fullPath2, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
			return false;
		}
		catch
		{
			return false;
		}
	}

	private static bool ContainsFileImplicitly(string csprojPath, string sourceFilePath, string projectRoot)
	{
		try
		{
			if (!IsUnityProject(csprojPath))
			{
				return false;
			}
			string fullPath = Path.GetFullPath(sourceFilePath);
			string fullPath2 = Path.GetFullPath(Path.Combine(projectRoot, "Assets"));
			string fullPath3 = Path.GetFullPath(Path.Combine(projectRoot, "Packages"));
			if (fullPath.StartsWith(fullPath2, StringComparison.OrdinalIgnoreCase) || fullPath.StartsWith(fullPath3, StringComparison.OrdinalIgnoreCase))
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

	private static bool IsUnityProject(string csprojPath)
	{
		string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(csprojPath);
		if (!fileNameWithoutExtension.StartsWith("Assembly-", StringComparison.OrdinalIgnoreCase))
		{
			return fileNameWithoutExtension.Contains("Unity", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}
}
