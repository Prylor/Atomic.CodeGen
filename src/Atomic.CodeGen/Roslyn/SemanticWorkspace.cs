using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Atomic.CodeGen.Core.Models;
using Atomic.CodeGen.Utils;
using Buildalyzer;
using Buildalyzer.Workspaces;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;

namespace Atomic.CodeGen.Roslyn;

public sealed class SemanticWorkspace : IDisposable
{
	private static bool _msbuildRegistered;

	private static bool _msbuildAvailable;

	private static readonly object _lockObj = new object();

	private Workspace? _workspace;

	private Solution? _solution;

	private readonly string _solutionPath;

	private readonly AnalyzerMode _analyzerMode;

	private readonly HashSet<string>? _includedProjects;

	private bool _usingBuildalyzer;

	public bool IsLoaded => _solution != null;

	public Solution? Solution => _solution;

	public IEnumerable<Project> Projects => _solution?.Projects ?? Enumerable.Empty<Project>();

	public bool UsingBuildalyzer => _usingBuildalyzer;

	public SemanticWorkspace(string solutionPath, AnalyzerMode analyzerMode = AnalyzerMode.Auto, IEnumerable<string>? includedProjects = null)
	{
		_solutionPath = solutionPath;
		_analyzerMode = analyzerMode;
		_includedProjects = ((includedProjects != null && includedProjects.Any()) ? new HashSet<string>(includedProjects, StringComparer.OrdinalIgnoreCase) : null);
	}

	private bool ShouldIncludeProject(string projectName)
	{
		if (_includedProjects == null)
		{
			return true;
		}
		if (!_includedProjects.Contains(projectName))
		{
			return _includedProjects.Contains(Path.GetFileNameWithoutExtension(projectName));
		}
		return true;
	}

	private static bool TryRegisterMSBuild()
	{
		lock (_lockObj)
		{
			if (_msbuildRegistered)
			{
				return _msbuildAvailable;
			}
			_msbuildRegistered = true;
			try
			{
				List<VisualStudioInstance> instances = MSBuildLocator.QueryVisualStudioInstances().ToList();
				if (instances.Count == 0)
				{
					Logger.LogVerbose("No MSBuild instances found, will use Buildalyzer.");
					_msbuildAvailable = false;
					return false;
				}
				VisualStudioInstance visualStudioInstance = (from i in instances
					orderby i.DiscoveryType == DiscoveryType.VisualStudioSetup descending, i.Version descending
					select i).First();
				MSBuildLocator.RegisterInstance(visualStudioInstance);
				Logger.LogVerbose($"Registered MSBuild: {visualStudioInstance.Name} {visualStudioInstance.Version}");
				_msbuildAvailable = true;
				return true;
			}
			catch (Exception ex)
			{
				Logger.LogVerbose("MSBuild registration failed: " + ex.Message);
				_msbuildAvailable = false;
				return false;
			}
		}
	}

	public async Task<bool> LoadAsync()
	{
		Logger.LogVerbose("Loading solution: " + _solutionPath);
		Logger.LogVerbose($"Analyzer mode: {_analyzerMode}");
		switch (_analyzerMode)
		{
		case AnalyzerMode.MSBuild:
			if (!TryRegisterMSBuild())
			{
				Logger.LogError("MSBuild not available. Install Visual Studio or .NET SDK, or change analyzerMode to 'Auto' or 'Buildalyzer'.");
				return false;
			}
			if (await TryLoadWithMSBuildAsync())
			{
				Logger.LogInfo("Using MSBuild for semantic analysis (forced).");
				return true;
			}
			Logger.LogError("MSBuild failed to load solution.");
			return false;
		case AnalyzerMode.Buildalyzer:
			if (TryLoadWithBuildalyzer())
			{
				Logger.LogInfo("Using Buildalyzer for semantic analysis (forced).");
				return true;
			}
			Logger.LogError("Buildalyzer failed to load solution.");
			return false;
		default:
			if (TryRegisterMSBuild())
			{
				if (await TryLoadWithMSBuildAsync())
				{
					Logger.LogInfo("Using MSBuild for semantic analysis.");
					return true;
				}
				Logger.LogVerbose("MSBuild workspace failed, falling back to Buildalyzer...");
			}
			if (TryLoadWithBuildalyzer())
			{
				Logger.LogInfo("Using Buildalyzer for semantic analysis (no VS/SDK required).");
				return true;
			}
			Logger.LogError("Failed to load solution with both MSBuild and Buildalyzer.");
			return false;
		}
	}

	private async Task<bool> TryLoadWithMSBuildAsync()
	{
		try
		{
			MSBuildWorkspace msbuildWorkspace = MSBuildWorkspace.Create();
			msbuildWorkspace.WorkspaceFailed += (sender, args) =>
			{
				if (args.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure)
				{
					Logger.LogVerbose("MSBuild Workspace: " + args.Diagnostic.Message);
				}
			};
			_solution = await msbuildWorkspace.OpenSolutionAsync(_solutionPath);
			_workspace = msbuildWorkspace;
			_usingBuildalyzer = false;
			int loadedProjectCount = _solution.Projects.Count();
			Logger.LogVerbose($"MSBuild loaded {loadedProjectCount} projects");
			return loadedProjectCount > 0;
		}
		catch (Exception ex)
		{
			Logger.LogVerbose("MSBuild load failed: " + ex.Message);
			return false;
		}
	}

	private bool TryLoadWithBuildalyzer()
	{
		try
		{
			Logger.LogVerbose("Initializing Buildalyzer...");
			AnalyzerManager analyzerManager = new AnalyzerManager(_solutionPath);
			List<IProjectAnalyzer> filteredProjects = analyzerManager.Projects.Values.Where((IProjectAnalyzer p) => ShouldIncludeProject(p.ProjectFile.Name)).ToList();
			int totalProjectCount = analyzerManager.Projects.Count;
			int filteredProjectCount = filteredProjects.Count;
			if (_includedProjects != null)
			{
				Logger.LogInfo($"Loading {filteredProjectCount}/{totalProjectCount} projects with Buildalyzer (filtered)...");
			}
			else
			{
				Logger.LogInfo($"Loading {totalProjectCount} projects with Buildalyzer (this may take a moment)...");
			}
			int processedCount = 0;
			AdhocWorkspace adhocWorkspace = new AdhocWorkspace();
			foreach (IProjectAnalyzer projectAnalyzer in filteredProjects)
			{
				processedCount++;
				Logger.LogVerbose($"  [{processedCount}/{filteredProjectCount}] {projectAnalyzer.ProjectFile.Name}");
				try
				{
					projectAnalyzer.Build().FirstOrDefault()?.AddToWorkspace(adhocWorkspace);
				}
				catch (Exception ex)
				{
					Logger.LogVerbose("    Warning: " + ex.Message);
				}
			}
			_solution = adhocWorkspace.CurrentSolution;
			_workspace = adhocWorkspace;
			_usingBuildalyzer = true;
			int loadedProjectCount = _solution.Projects.Count();
			Logger.LogVerbose($"Buildalyzer loaded {loadedProjectCount} projects");
			return loadedProjectCount > 0;
		}
		catch (Exception ex2)
		{
			Logger.LogVerbose("Buildalyzer load failed: " + ex2.Message);
			return false;
		}
	}

	public Project? FindProjectContainingFile(string filePath)
	{
		if (_solution == null)
		{
			return null;
		}
		string fullPath = Path.GetFullPath(filePath);
		foreach (Project project in _solution.Projects)
		{
			foreach (Document document in project.Documents)
			{
				if (document.FilePath != null && Path.GetFullPath(document.FilePath).Equals(fullPath, StringComparison.OrdinalIgnoreCase))
				{
					return project;
				}
			}
		}
		return null;
	}

	public async Task<List<INamedTypeSymbol>> GetDefinedTypesAsync(IEnumerable<string> filePaths)
	{
		List<INamedTypeSymbol> types = new List<INamedTypeSymbol>();
		if (_solution == null)
		{
			return types;
		}
		HashSet<string> normalizedPaths = filePaths.Select((string p) => Path.GetFullPath(p)).ToHashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (Project project in _solution.Projects)
		{
			Compilation compilation = await project.GetCompilationAsync();
			if (compilation == null)
			{
				continue;
			}
			foreach (Document document in project.Documents)
			{
				if (document.FilePath == null || !normalizedPaths.Contains(Path.GetFullPath(document.FilePath)))
				{
					continue;
				}
				SyntaxTree syntaxTree = await document.GetSyntaxTreeAsync();
				if (syntaxTree == null)
				{
					continue;
				}
				SemanticModel semanticModel = compilation.GetSemanticModel(syntaxTree);
				foreach (TypeDeclarationSyntax typeDecl in (await syntaxTree.GetRootAsync()).DescendantNodes().OfType<TypeDeclarationSyntax>())
				{
					INamedTypeSymbol declaredSymbol = semanticModel.GetDeclaredSymbol(typeDecl);
					if (declaredSymbol != null)
					{
						types.Add(declaredSymbol);
					}
				}
			}
		}
		return types;
	}

	public async Task<List<ReferenceLocation>> FindReferencesAsync(IEnumerable<ISymbol> symbols, IEnumerable<string>? limitToFiles = null)
	{
		List<ReferenceLocation> references = new List<ReferenceLocation>();
		if (_solution == null)
		{
			return references;
		}
		HashSet<string> limitToFilesSet = limitToFiles?.Select((string p) => Path.GetFullPath(p)).ToHashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (ISymbol symbol in symbols)
		{
			try
			{
				foreach (ReferencedSymbol referencedSymbol in await SymbolFinder.FindReferencesAsync(symbol, _solution))
				{
					foreach (ReferenceLocation location in referencedSymbol.Locations)
					{
						string filePath = location.Document.FilePath;
						if (filePath != null && (limitToFilesSet == null || limitToFilesSet.Contains(Path.GetFullPath(filePath))))
						{
							references.Add(location);
						}
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogVerbose("Error finding references for " + symbol.Name + ": " + ex.Message);
			}
		}
		return references;
	}

	public async Task<List<SemanticUsage>> FindUsagesOfTypesFromFilesAsync(IEnumerable<string> definitionFiles, IEnumerable<string>? searchInFiles = null)
	{
		List<SemanticUsage> usages = new List<SemanticUsage>();
		if (_solution == null)
		{
			return usages;
		}
		List<INamedTypeSymbol> definedTypes = await GetDefinedTypesAsync(definitionFiles);
		if (definedTypes.Count == 0)
		{
			Logger.LogVerbose("No types found in definition files");
			return usages;
		}
		Logger.LogVerbose($"Found {definedTypes.Count} defined types");
		foreach (ReferenceLocation refLocation in await FindReferencesAsync(definedTypes, searchInFiles))
		{
			Document doc = refLocation.Document;
			TextSpan span = refLocation.Location.SourceSpan;
			SourceText sourceText = await doc.GetTextAsync();
			LinePositionSpan linePositionSpan = sourceText.Lines.GetLinePositionSpan(span);
			usages.Add(new SemanticUsage
			{
				FilePath = doc.FilePath,
				Line = linePositionSpan.Start.Line + 1,
				Column = linePositionSpan.Start.Character + 1,
				Length = span.Length,
				SymbolName = sourceText.GetSubText(span).ToString()
			});
		}
		return usages;
	}

	public string? GetProjectForFile(string filePath)
	{
		return FindProjectContainingFile(filePath)?.FilePath;
	}

	public void Dispose()
	{
		_workspace?.Dispose();
	}
}
