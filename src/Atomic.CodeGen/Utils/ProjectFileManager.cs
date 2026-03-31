using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Atomic.CodeGen.Utils;

public static class ProjectFileManager
{
	public static async Task AddGeneratedFileAsync(string projectFilePath, string generatedFilePath, string projectRoot)
	{
		if (!File.Exists(projectFilePath))
		{
			Logger.LogWarning("Project file not found: " + projectFilePath);
			return;
		}
		try
		{
			XDocument xDocument = XDocument.Load(projectFilePath);
			XElement root = xDocument.Root;
			if (root == null || root.Name.LocalName != "Project")
			{
				Logger.LogWarning("Invalid project file format: " + projectFilePath);
				return;
			}
			string relativeTo = Path.GetDirectoryName(projectFilePath) ?? projectRoot;
			string relativePath = Path.GetRelativePath(relativeTo, generatedFilePath);
			if ((from e in root.Descendants()
				where e.Name.LocalName == "Compile"
				select e).FirstOrDefault((XElement e) => e.Attribute("Include")?.Value == relativePath) != null)
			{
				Logger.LogVerbose("File already in project: " + relativePath);
				return;
			}
			XElement xElement = root.Descendants().FirstOrDefault((XElement e) => e.Name.LocalName == "ItemGroup" && e.Elements().Any((XElement el) => el.Name.LocalName == "Compile") && e.Attribute("xmlns") == null);
			if (xElement == null)
			{
				xElement = new XElement(root.Name.Namespace + "ItemGroup");
				root.Add(xElement);
			}
			XElement xElement2 = new XElement(root.Name.Namespace + "Compile");
			xElement2.SetAttributeValue("Include", relativePath);
			xElement.Add(xElement2);
			await File.WriteAllTextAsync(projectFilePath, xDocument.ToString());
			Logger.LogVerbose("Added to project: " + relativePath);
		}
		catch (Exception ex)
		{
			Logger.LogWarning("Failed to add file to project: " + ex.Message);
		}
	}

	public static async Task RemoveFileFromProjectAsync(string projectFilePath, string fileToRemove, string projectRoot)
	{
		if (!File.Exists(projectFilePath))
		{
			return;
		}
		try
		{
			XDocument xDocument = XDocument.Load(projectFilePath);
			XElement root = xDocument.Root;
			if (root != null && root.Name.LocalName == "Project")
			{
				string relativeTo = Path.GetDirectoryName(projectFilePath) ?? projectRoot;
				string relativePath = Path.GetRelativePath(relativeTo, fileToRemove);
				XElement xElement = (from e in root.Descendants()
					where e.Name.LocalName == "Compile"
					select e).FirstOrDefault((XElement e) => e.Attribute("Include")?.Value == relativePath);
				if (xElement != null)
				{
					xElement.Remove();
					await File.WriteAllTextAsync(projectFilePath, xDocument.ToString());
					Logger.LogVerbose("Removed from project " + Path.GetFileName(projectFilePath) + ": " + relativePath);
				}
			}
		}
		catch (Exception ex)
		{
			Logger.LogWarning("Failed to remove file from project: " + ex.Message);
		}
	}

	public static async Task RemoveGeneratedFilesAsync(string projectFilePath, string outputDirectory, string projectRoot)
	{
		if (!File.Exists(projectFilePath))
		{
			return;
		}
		try
		{
			XDocument xDocument = XDocument.Load(projectFilePath);
			XElement root = xDocument.Root;
			if (root == null || root.Name.LocalName != "Project")
			{
				return;
			}
			string path = Path.GetDirectoryName(projectFilePath) ?? projectRoot;
			string fullPath = Path.GetFullPath(Path.Combine(projectRoot, outputDirectory));
			List<XElement> list = (from e in root.Descendants()
				where e.Name.LocalName == "Compile"
				select e).ToList();
			bool flag = false;
			foreach (XElement item in list)
			{
				string text = item.Attribute("Include")?.Value;
				if (!string.IsNullOrEmpty(text) && Path.GetFullPath(Path.Combine(path, text)).StartsWith(fullPath, StringComparison.OrdinalIgnoreCase))
				{
					item.Remove();
					flag = true;
					Logger.LogVerbose("Removed from project: " + text);
				}
			}
			if (flag)
			{
				await File.WriteAllTextAsync(projectFilePath, xDocument.ToString());
			}
		}
		catch (Exception ex)
		{
			Logger.LogWarning("Failed to remove generated files from project: " + ex.Message);
		}
	}
}
