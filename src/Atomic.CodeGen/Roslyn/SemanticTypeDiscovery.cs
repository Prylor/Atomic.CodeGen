using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Atomic.CodeGen.Core.Models;
using Atomic.CodeGen.Core.Models.EntityDomain;
using Atomic.CodeGen.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Atomic.CodeGen.Roslyn;

public sealed class SemanticTypeDiscovery : IDisposable
{
	private readonly string _solutionPath;

	private readonly AnalyzerMode _analyzerMode;

	private readonly IEnumerable<string>? _includedProjects;

	private SemanticWorkspace? _workspace;

	private const string EntityAPIAttributeName = "Atomic.Entities.EntityAPIAttribute";

	private const string EntityAPIAttributeShortName = "EntityAPI";

	private const string LinkToAttributeName = "Atomic.Entities.LinkToAttribute";

	private const string LinkToAttributeShortName = "LinkTo";

	private const string EntityDomainBuilderName = "EntityDomainBuilder";

	private const string IEntityDomainName = "IEntityDomain";

	private static readonly Regex PreprocessorSymbolRegex = new Regex("#(?:if|elif)\\s+(!?\\s*)?(\\w+)", RegexOptions.Multiline | RegexOptions.Compiled);

	private static readonly HashSet<string> ValidBehaviourBases = new HashSet<string>(StringComparer.Ordinal) { "IEntityBehaviour", "IEntityInit", "IEntityDispose", "IEntityEnable", "IEntityDisable", "IEntityTick", "IEntityFixedTick", "IEntityLateTick", "IEntityGizmos" };

	public SemanticTypeDiscovery(string solutionPath, AnalyzerMode analyzerMode = AnalyzerMode.Auto, IEnumerable<string>? includedProjects = null)
	{
		_solutionPath = solutionPath;
		_analyzerMode = analyzerMode;
		_includedProjects = includedProjects;
	}

	public async Task<DiscoveryResult> DiscoverAllAsync(IEnumerable<string>? excludePaths = null)
	{
		DiscoveryResult result = new DiscoveryResult();
		_workspace = new SemanticWorkspace(_solutionPath, _analyzerMode, _includedProjects);
		try
		{
			if (!(await _workspace.LoadAsync()))
			{
				Logger.LogWarning("Failed to load solution for type discovery.");
				return result;
			}
			List<string> excludePatterns = excludePaths?.ToList() ?? new List<string>();
			foreach (Project project in _workspace.Projects)
			{
				Compilation compilation = await project.GetCompilationAsync();
				if (compilation == null)
				{
					continue;
				}
				foreach (Document document in project.Documents)
				{
					if (document.FilePath != null && !ShouldExclude(document.FilePath, excludePatterns))
					{
						await ProcessDocumentAsync(document, compilation, result);
					}
				}
			}
			Logger.LogVerbose($"Discovered {result.EntityApis.Count} EntityAPIs, {result.Behaviours.Count} Behaviours, {result.Domains.Count} Domains");
			return result;
		}
		finally
		{
			_workspace?.Dispose();
			_workspace = null;
		}
	}

	public async Task<List<EntityAPIDefinition>> DiscoverEntityApisAsync(IEnumerable<string>? excludePaths = null)
	{
		return (await DiscoverAllAsync(excludePaths)).EntityApis;
	}

	public async Task<List<BehaviourDefinition>> DiscoverBehavioursAsync(IEnumerable<string>? excludePaths = null)
	{
		return (await DiscoverAllAsync(excludePaths)).Behaviours;
	}

	public async Task<Dictionary<string, EntityDomainDefinition>> DiscoverDomainsAsync(IEnumerable<string>? excludePaths = null)
	{
		return (await DiscoverAllAsync(excludePaths)).Domains;
	}

	public async Task<(bool HasEntityApiAttribute, bool HasLinkToAttribute, bool HasEntityDomainBuilder)> CheckAtomicFrameworkAsync()
	{
		_workspace = new SemanticWorkspace(_solutionPath, _analyzerMode, _includedProjects);
		try
		{
			if (!(await _workspace.LoadAsync()))
			{
				return (HasEntityApiAttribute: false, HasLinkToAttribute: false, HasEntityDomainBuilder: false);
			}
			bool hasEntityApiAttribute = false;
			bool hasLinkToAttribute = false;
			bool hasEntityDomainBuilder = false;
			foreach (Project project in _workspace.Projects)
			{
				foreach (Document document in project.Documents)
				{
					if (document.FilePath == null)
					{
						continue;
					}
					SyntaxTree syntaxTree = await document.GetSyntaxTreeAsync();
					if (syntaxTree == null)
					{
						continue;
					}
					foreach (ClassDeclarationSyntax classDecl in (await syntaxTree.GetRootAsync()).DescendantNodes().OfType<ClassDeclarationSyntax>())
					{
						string className = classDecl.Identifier.Text;
						if (className == "EntityAPIAttribute" && InheritsFromAttribute(classDecl))
						{
							hasEntityApiAttribute = true;
						}
						if (className == "LinkToAttribute" && InheritsFromAttribute(classDecl))
						{
							hasLinkToAttribute = true;
						}
						if (className == EntityDomainBuilderName && classDecl.Modifiers.Any((SyntaxToken m) => m.IsKind(SyntaxKind.AbstractKeyword)))
						{
							hasEntityDomainBuilder = true;
						}
						if (hasEntityApiAttribute && hasLinkToAttribute && hasEntityDomainBuilder)
						{
							return (HasEntityApiAttribute: true, HasLinkToAttribute: true, HasEntityDomainBuilder: true);
						}
					}
				}
			}
			return (HasEntityApiAttribute: hasEntityApiAttribute, HasLinkToAttribute: hasLinkToAttribute, HasEntityDomainBuilder: hasEntityDomainBuilder);
		}
		finally
		{
			_workspace?.Dispose();
			_workspace = null;
		}
	}

	private static bool InheritsFromAttribute(ClassDeclarationSyntax classDecl)
	{
		if (classDecl.BaseList == null)
		{
			return false;
		}
		foreach (BaseTypeSyntax baseType in classDecl.BaseList.Types)
		{
			string baseTypeName = baseType.Type.ToString();
			if (baseTypeName == "Attribute" || baseTypeName == "System.Attribute")
			{
				return true;
			}
		}
		return false;
	}

	private async Task ProcessDocumentAsync(Document doc, Compilation compilation, DiscoveryResult result)
	{
		SyntaxTree syntaxTree = await doc.GetSyntaxTreeAsync();
		if (syntaxTree == null || !(await syntaxTree.GetRootAsync() is CompilationUnitSyntax compilationUnitSyntax))
		{
			return;
		}
		SemanticModel semanticModel = compilation.GetSemanticModel(syntaxTree);
		string filePath = doc.FilePath;
		bool hasAtomicEntitiesUsing = compilationUnitSyntax.Usings.Any((UsingDirectiveSyntax u) => u.Name?.ToString() == "Atomic.Entities");
		foreach (ClassDeclarationSyntax classDecl in compilationUnitSyntax.DescendantNodes().OfType<ClassDeclarationSyntax>())
		{
			INamedTypeSymbol declaredSymbol = semanticModel.GetDeclaredSymbol(classDecl);
			if (declaredSymbol == null)
			{
				continue;
			}
			if (HasAttribute(declaredSymbol, EntityAPIAttributeName, EntityAPIAttributeShortName, hasAtomicEntitiesUsing))
			{
				EntityAPIDefinition entityAPIDefinition = ParseEntityAPI(filePath, compilationUnitSyntax, classDecl, hasAtomicEntitiesUsing);
				if (entityAPIDefinition != null)
				{
					result.EntityApis.Add(entityAPIDefinition);
				}
			}
			if (HasAttribute(declaredSymbol, LinkToAttributeName, LinkToAttributeShortName, hasAtomicEntitiesUsing))
			{
				BehaviourDefinition behaviourDefinition = ParseBehaviour(filePath, compilationUnitSyntax, classDecl, hasAtomicEntitiesUsing);
				if (behaviourDefinition != null)
				{
					result.Behaviours.Add(behaviourDefinition);
				}
			}
			if (InheritsFrom(declaredSymbol, EntityDomainBuilderName) || ImplementsInterface(declaredSymbol, IEntityDomainName))
			{
				EntityDomainDefinition entityDomainDefinition = ParseDomain(filePath, compilationUnitSyntax, classDecl);
				if (entityDomainDefinition != null)
				{
					result.Domains[filePath] = entityDomainDefinition;
				}
			}
		}
	}

	private bool HasAttribute(INamedTypeSymbol symbol, string fullName, string shortName, bool hasUsing)
	{
		foreach (AttributeData attribute in symbol.GetAttributes())
		{
			string attributeFullName = attribute.AttributeClass?.ToDisplayString();
			if (attributeFullName != null)
			{
				if (attributeFullName == fullName)
				{
					return true;
				}
				if (hasUsing && (attributeFullName.EndsWith("." + shortName) || attributeFullName.EndsWith("." + shortName + "Attribute")))
				{
					return true;
				}
			}
		}
		return false;
	}

	private bool InheritsFrom(INamedTypeSymbol symbol, string baseClassName)
	{
		for (INamedTypeSymbol baseType = symbol.BaseType; baseType != null; baseType = baseType.BaseType)
		{
			if (baseType.Name == baseClassName)
			{
				return true;
			}
		}
		return false;
	}

	private bool ImplementsInterface(INamedTypeSymbol symbol, string interfaceName)
	{
		return symbol.AllInterfaces.Any((INamedTypeSymbol i) => i.Name == interfaceName);
	}

	private EntityAPIDefinition? ParseEntityAPI(string filePath, CompilationUnitSyntax root, ClassDeclarationSyntax classDecl, bool hasAtomicEntitiesUsing)
	{
		AttributeSyntax attribute = GetAttribute(classDecl, EntityAPIAttributeShortName, hasAtomicEntitiesUsing);
		if (attribute == null)
		{
			return null;
		}
		Dictionary<string, object> args = AttributeParser.Parse(attribute);
		List<string> tags = TypeExtractor.ExtractTags(classDecl);
		Dictionary<string, string> values = TypeExtractor.ExtractValues(classDecl);
		string[] stringArray = AttributeParser.GetStringArray(args, "ExcludeImports");
		List<string> imports = ImportExtractor.Extract(root, stringArray);
		string sourceNamespace = GetNamespace(classDecl);
		string sourceClassName = classDecl.Identifier.Text;
		string sourceDirectory = Path.GetDirectoryName(filePath) ?? string.Empty;
		string overrideNamespace = AttributeParser.GetString(args, "Namespace");
		string overrideClassName = AttributeParser.GetString(args, "ClassName");
		string overrideDirectory = AttributeParser.GetString(args, "Directory");
		EntityAPIDefinition entityAPIDefinition = new EntityAPIDefinition
		{
			SourceFile = filePath,
			Namespace = ((!string.IsNullOrWhiteSpace(overrideNamespace)) ? overrideNamespace : sourceNamespace),
			ClassName = ((!string.IsNullOrWhiteSpace(overrideClassName)) ? overrideClassName : sourceClassName),
			Directory = ((!string.IsNullOrWhiteSpace(overrideDirectory)) ? overrideDirectory : sourceDirectory),
			TargetProject = AttributeParser.GetString(args, "TargetProject"),
			EntityType = AttributeParser.GetTypeName(args, "EntityType"),
			AggressiveInlining = AttributeParser.GetBool(args, "AggressiveInlining", defaultValue: true),
			UnsafeAccess = AttributeParser.GetBool(args, "UnsafeAccess", defaultValue: true),
			Imports = imports,
			Tags = tags,
			Values = values
		};
		entityAPIDefinition.Validate();
		if (!entityAPIDefinition.IsValid)
		{
			Logger.LogError("Invalid EntityAPI definition in " + filePath + ":");
			foreach (string error in entityAPIDefinition.Errors)
			{
				Logger.LogError("  - " + error);
			}
			return null;
		}
		Logger.LogVerbose("Discovered EntityAPI: " + entityAPIDefinition.ClassName + " from " + filePath);
		return entityAPIDefinition;
	}

	private BehaviourDefinition? ParseBehaviour(string filePath, CompilationUnitSyntax root, ClassDeclarationSyntax classDecl, bool hasAtomicEntitiesUsing)
	{
		AttributeSyntax attribute = GetAttribute(classDecl, LinkToAttributeShortName, hasAtomicEntitiesUsing);
		if (attribute == null)
		{
			return null;
		}
		string linkedApiTypeName = ExtractLinkedApiTypeName(attribute);
		if (string.IsNullOrEmpty(linkedApiTypeName))
		{
			Logger.LogWarning("[LinkTo] attribute in " + filePath + " has no valid type argument");
			return null;
		}
		string className = classDecl.Identifier.Text;
		string namespaceName = GetNamespace(classDecl);
		List<(string, string)> constructorParameters = ExtractConstructorParameters(classDecl);
		List<string> requiredImports = ImportExtractor.Extract(root, Array.Empty<string>());
		BehaviourDefinition behaviourDefinition = new BehaviourDefinition
		{
			SourceFile = filePath,
			LinkedApiTypeName = linkedApiTypeName,
			Namespace = namespaceName,
			ClassName = className,
			ConstructorParameters = constructorParameters,
			RequiredImports = requiredImports
		};
		ValidateBehaviourClass(classDecl, behaviourDefinition);
		if (!behaviourDefinition.IsValid)
		{
			foreach (string error in behaviourDefinition.Errors)
			{
				Logger.LogError($"Behaviour {className} in {filePath}: {error}");
			}
			return null;
		}
		Logger.LogVerbose($"Discovered Behaviour: {className} -> {linkedApiTypeName} from {filePath}");
		return behaviourDefinition;
	}

	private void ValidateBehaviourClass(ClassDeclarationSyntax classDecl, BehaviourDefinition definition)
	{
		if (classDecl.BaseList == null || !classDecl.BaseList.Types.Any())
		{
			definition.Errors.Add("Class must implement IEntityBehaviour or a derived interface");
			return;
		}
		if (classDecl.Modifiers.Any((SyntaxToken m) => m.IsKind(SyntaxKind.AbstractKeyword)))
		{
			definition.Errors.Add("Behaviour class cannot be abstract");
		}
		if (classDecl.Modifiers.Any((SyntaxToken m) => m.IsKind(SyntaxKind.StaticKeyword)))
		{
			definition.Errors.Add("Behaviour class cannot be static");
		}
	}

	private string ExtractLinkedApiTypeName(AttributeSyntax attribute)
	{
		SeparatedSyntaxList<AttributeArgumentSyntax>? separatedSyntaxList = attribute.ArgumentList?.Arguments;
		if (!separatedSyntaxList.HasValue || separatedSyntaxList.Value.Count == 0)
		{
			return string.Empty;
		}
		if (separatedSyntaxList.Value[0].Expression is TypeOfExpressionSyntax typeOfExpressionSyntax)
		{
			return typeOfExpressionSyntax.Type.ToString();
		}
		return string.Empty;
	}

	private List<(string Name, string Type)> ExtractConstructorParameters(ClassDeclarationSyntax classDecl)
	{
		List<(string, string)> parameters = new List<(string, string)>();
		List<ConstructorDeclarationSyntax> allConstructors = classDecl.Members.OfType<ConstructorDeclarationSyntax>().ToList();
		if (allConstructors.Count == 0)
		{
			return parameters;
		}
		List<ConstructorDeclarationSyntax> publicConstructors = allConstructors.Where((ConstructorDeclarationSyntax c) => c.Modifiers.Any((SyntaxToken m) => m.IsKind(SyntaxKind.PublicKeyword))).ToList();
		ConstructorDeclarationSyntax selectedConstructor = null;
		if (publicConstructors.Count > 0)
		{
			selectedConstructor = publicConstructors.OrderByDescending((ConstructorDeclarationSyntax c) => c.ParameterList.Parameters.Count).First();
		}
		else if (allConstructors.Count == 1)
		{
			selectedConstructor = allConstructors[0];
		}
		if (selectedConstructor != null && selectedConstructor.ParameterList.Parameters.Count > 0)
		{
			foreach (ParameterSyntax param in selectedConstructor.ParameterList.Parameters)
			{
				string paramName = param.Identifier.Text;
				string paramType = param.Type?.ToString() ?? "object";
				parameters.Add((paramName, paramType));
			}
		}
		return parameters;
	}

	private EntityDomainDefinition? ParseDomain(string filePath, CompilationUnitSyntax root, ClassDeclarationSyntax classDecl)
	{
		try
		{
			EntityDomainDefinition entityDomainDefinition = new EntityDomainDefinition
			{
				SourceFile = filePath,
				ClassName = classDecl.Identifier.Text
			};
			entityDomainDefinition.DetectedImports = (from u in root.Usings
				select u.Name?.ToString() into name
				where !string.IsNullOrWhiteSpace(name)
				select (name)).ToList();
			string propertyValue = GetPropertyValue(classDecl, "EntityName");
			if (string.IsNullOrEmpty(propertyValue))
			{
				Logger.LogWarning("EntityName is required in " + classDecl.Identifier.Text);
				return null;
			}
			entityDomainDefinition.EntityName = propertyValue;
			string propertyValue2 = GetPropertyValue(classDecl, "Namespace");
			if (string.IsNullOrEmpty(propertyValue2))
			{
				Logger.LogWarning("Namespace is required in " + classDecl.Identifier.Text);
				return null;
			}
			entityDomainDefinition.Namespace = propertyValue2;
			entityDomainDefinition.Directory = GetPropertyValue(classDecl, "Directory") ?? ("Assets/Scripts/Generated/" + entityDomainDefinition.EntityName);
			MethodDeclarationSyntax methodDeclarationSyntax = classDecl.Members.OfType<MethodDeclarationSyntax>().FirstOrDefault((MethodDeclarationSyntax m) => m.Identifier.Text == "Configure");
			if (methodDeclarationSyntax?.Body != null)
			{
				ParseConfigureMethodCalls(methodDeclarationSyntax.Body, entityDomainDefinition);
			}
			else
			{
				ParsePropertyBasedConfig(classDecl, entityDomainDefinition);
			}
			Logger.LogVerbose("Discovered Domain: " + entityDomainDefinition.EntityName + " from " + filePath);
			return entityDomainDefinition;
		}
		catch (Exception ex)
		{
			Logger.LogError("Failed to parse domain " + classDecl.Identifier.Text + ": " + ex.Message);
			return null;
		}
	}

	private void ParseConfigureMethodCalls(BlockSyntax methodBody, EntityDomainDefinition definition)
	{
		IEnumerable<InvocationExpressionSyntax> invocations = methodBody.DescendantNodes().OfType<InvocationExpressionSyntax>();
		int modeCount = 0;
		foreach (InvocationExpressionSyntax invocation in invocations)
		{
			switch (GetMethodName(invocation))
			{
			case "EntityMode":
				definition.Mode = EntityMode.Entity;
				modeCount++;
				break;
			case "EntitySingletonMode":
				definition.Mode = EntityMode.EntitySingleton;
				modeCount++;
				break;
			case "SceneEntityMode":
				definition.Mode = EntityMode.SceneEntity;
				modeCount++;
				break;
			case "SceneEntitySingletonMode":
				definition.Mode = EntityMode.SceneEntitySingleton;
				modeCount++;
				break;
			case "GenerateProxy":
				definition.GenerateProxy = true;
				break;
			case "GenerateWorld":
				definition.GenerateWorld = true;
				break;
			case "IEntityInstaller":
				definition.Installers |= EntityInstallerMode.IEntityInstaller;
				break;
			case "ScriptableEntityInstaller":
				definition.Installers |= EntityInstallerMode.ScriptableEntityInstaller;
				break;
			case "SceneEntityInstaller":
				definition.Installers |= EntityInstallerMode.SceneEntityInstaller;
				break;
			case "ScriptableEntityAspect":
				definition.Aspects |= EntityAspectMode.ScriptableEntityAspect;
				break;
			case "SceneEntityAspect":
				definition.Aspects |= EntityAspectMode.SceneEntityAspect;
				break;
			case "SceneEntityPool":
				definition.Pools |= EntityPoolMode.SceneEntityPool;
				break;
			case "PrefabEntityPool":
				definition.Pools |= EntityPoolMode.PrefabEntityPool;
				break;
			case "ScriptableEntityFactory":
				definition.Factories |= EntityFactoryMode.ScriptableEntityFactory;
				break;
			case "SceneEntityFactory":
				definition.Factories |= EntityFactoryMode.SceneEntityFactory;
				break;
			case "StandardBaker":
				definition.Bakers |= EntityBakerMode.Standard;
				break;
			case "OptimizedBaker":
				definition.Bakers |= EntityBakerMode.Optimized;
				break;
			case "EntityView":
				definition.Views |= EntityViewMode.EntityView;
				break;
			case "EntityViewCatalog":
				definition.Views |= EntityViewMode.EntityViewCatalog;
				break;
			case "EntityViewPool":
				definition.Views |= EntityViewMode.EntityViewPool;
				break;
			case "EntityCollectionView":
				definition.Views |= EntityViewMode.EntityCollectionView;
				break;
			case "ExcludeImports":
				definition.ExcludeImports = GetStringArrayArgument(invocation);
				break;
			case "TargetProject":
				definition.TargetProject = GetStringArgument(invocation) ?? "";
				break;
			}
		}
		if (modeCount == 0)
		{
			definition.Mode = EntityMode.Entity;
		}
		else if (modeCount > 1)
		{
			Logger.LogWarning("Multiple entity modes in " + definition.ClassName + ". Using first.");
		}
	}

	private void ParsePropertyBasedConfig(ClassDeclarationSyntax classDecl, EntityDomainDefinition definition)
	{
		definition.Mode = GetEnumPropertyValue<EntityMode>(classDecl, "Mode").GetValueOrDefault();
		bool? generateProxyOverride = GetBoolPropertyValue(classDecl, "GenerateProxy");
		EntityDomainDefinition targetDefinition = definition;
		bool? nullableBool = generateProxyOverride;
		bool resolvedValue;
		if (nullableBool.HasValue)
		{
			resolvedValue = nullableBool == true;
		}
		else
		{
			EntityMode mode = definition.Mode;
			bool isSceneMode = mode == EntityMode.SceneEntity || mode == EntityMode.SceneEntitySingleton;
			resolvedValue = isSceneMode;
		}
		targetDefinition.GenerateProxy = resolvedValue;
		bool? generateWorldOverride = GetBoolPropertyValue(classDecl, "GenerateWorld");
		targetDefinition = definition;
		nullableBool = generateWorldOverride;
		if (nullableBool.HasValue)
		{
			resolvedValue = nullableBool == true;
		}
		else
		{
			EntityMode mode = definition.Mode;
			bool isSceneMode = mode == EntityMode.SceneEntity || mode == EntityMode.SceneEntitySingleton;
			resolvedValue = isSceneMode;
		}
		targetDefinition.GenerateWorld = resolvedValue;
		definition.Installers = GetEnumPropertyValue<EntityInstallerMode>(classDecl, "Installers").GetValueOrDefault();
		definition.Aspects = GetEnumPropertyValue<EntityAspectMode>(classDecl, "Aspects").GetValueOrDefault();
		definition.Pools = GetEnumPropertyValue<EntityPoolMode>(classDecl, "Pools").GetValueOrDefault();
		definition.Factories = GetEnumPropertyValue<EntityFactoryMode>(classDecl, "Factories").GetValueOrDefault();
		definition.Bakers = GetEnumPropertyValue<EntityBakerMode>(classDecl, "Bakers").GetValueOrDefault();
		definition.Views = GetEnumPropertyValue<EntityViewMode>(classDecl, "Views").GetValueOrDefault();
		definition.ExcludeImports = GetStringArrayPropertyValue(classDecl, "ExcludeImports");
		definition.TargetProject = GetPropertyValue(classDecl, "TargetProject");
	}

	private static string GetNamespace(ClassDeclarationSyntax classDecl)
	{
		SyntaxNode parent = classDecl.Parent;
		while (parent != null)
		{
			if (!(parent is NamespaceDeclarationSyntax namespaceDeclarationSyntax))
			{
				if (parent is FileScopedNamespaceDeclarationSyntax fileScopedNamespaceDeclarationSyntax)
				{
					return fileScopedNamespaceDeclarationSyntax.Name.ToString();
				}
				parent = parent.Parent;
				continue;
			}
			return namespaceDeclarationSyntax.Name.ToString();
		}
		return string.Empty;
	}

	private static AttributeSyntax? GetAttribute(ClassDeclarationSyntax classDecl, string shortName, bool hasUsing)
	{
		return classDecl.AttributeLists.SelectMany((AttributeListSyntax al) => al.Attributes).FirstOrDefault((AttributeSyntax a) =>
		{
			string attributeName = a.Name.ToString();
			if (attributeName.Contains("Atomic.Entities." + shortName))
			{
				return true;
			}
			return hasUsing && (attributeName == shortName || attributeName == shortName + "Attribute");
		});
	}

	private static string? GetMethodName(InvocationExpressionSyntax invocation)
	{
		if (invocation.Expression is IdentifierNameSyntax { Identifier: var identifier })
		{
			return identifier.Text;
		}
		if (invocation.Expression is MemberAccessExpressionSyntax memberAccessExpressionSyntax)
		{
			return memberAccessExpressionSyntax.Name.Identifier.Text;
		}
		return null;
	}

	private static string? GetStringArgument(InvocationExpressionSyntax invocation)
	{
		if (invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax { Token: { Value: string value } })
		{
			return value;
		}
		return null;
	}

	private static string[] GetStringArrayArgument(InvocationExpressionSyntax invocation)
	{
		if (invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression is ImplicitArrayCreationExpressionSyntax implicitArrayCreationExpressionSyntax)
		{
			return (from lit in implicitArrayCreationExpressionSyntax.Initializer.Expressions.OfType<LiteralExpressionSyntax>()
				where lit.Token.Value is string
				select (string)lit.Token.Value).ToArray();
		}
		return (from lit in invocation.ArgumentList.Arguments.Select((ArgumentSyntax arg) => arg.Expression).OfType<LiteralExpressionSyntax>()
			where lit.Token.Value is string
			select (string)lit.Token.Value).ToArray();
	}

	private static string? GetPropertyValue(ClassDeclarationSyntax classDecl, string propertyName)
	{
		PropertyDeclarationSyntax propertyDeclarationSyntax = classDecl.Members.OfType<PropertyDeclarationSyntax>().FirstOrDefault((PropertyDeclarationSyntax p) => p.Identifier.Text == propertyName);
		if (propertyDeclarationSyntax == null)
		{
			return null;
		}
		object expressionNode = propertyDeclarationSyntax.ExpressionBody?.Expression;
		if (expressionNode == null)
		{
			AccessorListSyntax? accessorList = propertyDeclarationSyntax.AccessorList;
			expressionNode = ((accessorList == null) ? null : accessorList.Accessors.FirstOrDefault((AccessorDeclarationSyntax a) => a.Kind() == SyntaxKind.GetAccessorDeclaration)?.ExpressionBody?.Expression);
		}
		if (expressionNode is LiteralExpressionSyntax { Token: { Value: string value } })
		{
			return value;
		}
		return null;
	}

	private static T? GetEnumPropertyValue<T>(ClassDeclarationSyntax classDecl, string propertyName) where T : struct, Enum
	{
		PropertyDeclarationSyntax propertyDeclarationSyntax = classDecl.Members.OfType<PropertyDeclarationSyntax>().FirstOrDefault((PropertyDeclarationSyntax p) => p.Identifier.Text == propertyName);
		if (propertyDeclarationSyntax == null)
		{
			return null;
		}
		object expressionNode = propertyDeclarationSyntax.ExpressionBody?.Expression;
		if (expressionNode == null)
		{
			AccessorListSyntax? accessorList = propertyDeclarationSyntax.AccessorList;
			expressionNode = ((accessorList == null) ? null : accessorList.Accessors.FirstOrDefault((AccessorDeclarationSyntax a) => a.Kind() == SyntaxKind.GetAccessorDeclaration)?.ExpressionBody?.Expression);
		}
		ExpressionSyntax expressionSyntax = (ExpressionSyntax)expressionNode;
		if (expressionSyntax is MemberAccessExpressionSyntax memberAccessExpressionSyntax && Enum.TryParse<T>(memberAccessExpressionSyntax.Name.Identifier.Text, out var result))
		{
			return result;
		}
		if (expressionSyntax is BinaryExpressionSyntax binaryExpressionSyntax && binaryExpressionSyntax.IsKind(SyntaxKind.BitwiseOrExpression))
		{
			return ParseFlagsExpression<T>(binaryExpressionSyntax);
		}
		return null;
	}

	private static T ParseFlagsExpression<T>(BinaryExpressionSyntax binaryExpr) where T : struct, Enum
	{
		List<T> flagValues = new List<T>();
		CollectFlagValues(binaryExpr, flagValues);
		int combinedFlags = 0;
		foreach (T flagValue in flagValues)
		{
			combinedFlags |= Convert.ToInt32(flagValue);
		}
		return (T)(object)combinedFlags;
	}

	private static void CollectFlagValues<T>(ExpressionSyntax expr, List<T> values) where T : struct, Enum
	{
		T result;
		if (expr is BinaryExpressionSyntax binaryExpressionSyntax && binaryExpressionSyntax.IsKind(SyntaxKind.BitwiseOrExpression))
		{
			CollectFlagValues(binaryExpressionSyntax.Left, values);
			CollectFlagValues(binaryExpressionSyntax.Right, values);
		}
		else if (expr is MemberAccessExpressionSyntax memberAccessExpressionSyntax && Enum.TryParse<T>(memberAccessExpressionSyntax.Name.Identifier.Text, out result))
		{
			values.Add(result);
		}
	}

	private static bool? GetBoolPropertyValue(ClassDeclarationSyntax classDecl, string propertyName)
	{
		PropertyDeclarationSyntax propertyDeclarationSyntax = classDecl.Members.OfType<PropertyDeclarationSyntax>().FirstOrDefault((PropertyDeclarationSyntax p) => p.Identifier.Text == propertyName);
		if (propertyDeclarationSyntax == null)
		{
			return null;
		}
		object expressionNode = propertyDeclarationSyntax.ExpressionBody?.Expression;
		if (expressionNode == null)
		{
			AccessorListSyntax? accessorList = propertyDeclarationSyntax.AccessorList;
			expressionNode = ((accessorList == null) ? null : accessorList.Accessors.FirstOrDefault((AccessorDeclarationSyntax a) => a.Kind() == SyntaxKind.GetAccessorDeclaration)?.ExpressionBody?.Expression);
		}
		if (expressionNode is LiteralExpressionSyntax node)
		{
			if (node.IsKind(SyntaxKind.TrueLiteralExpression))
			{
				return true;
			}
			if (node.IsKind(SyntaxKind.FalseLiteralExpression))
			{
				return false;
			}
		}
		return null;
	}

	private static string[]? GetStringArrayPropertyValue(ClassDeclarationSyntax classDecl, string propertyName)
	{
		PropertyDeclarationSyntax propertyDeclarationSyntax = classDecl.Members.OfType<PropertyDeclarationSyntax>().FirstOrDefault((PropertyDeclarationSyntax p) => p.Identifier.Text == propertyName);
		if (propertyDeclarationSyntax == null)
		{
			return null;
		}
		object expressionNode = propertyDeclarationSyntax.ExpressionBody?.Expression;
		if (expressionNode == null)
		{
			AccessorListSyntax? accessorList = propertyDeclarationSyntax.AccessorList;
			expressionNode = ((accessorList == null) ? null : accessorList.Accessors.FirstOrDefault((AccessorDeclarationSyntax a) => a.Kind() == SyntaxKind.GetAccessorDeclaration)?.ExpressionBody?.Expression);
		}
		ExpressionSyntax expressionSyntax = (ExpressionSyntax)expressionNode;
		if (expressionSyntax is ImplicitArrayCreationExpressionSyntax implicitArrayCreationExpressionSyntax)
		{
			return (from lit in implicitArrayCreationExpressionSyntax.Initializer.Expressions.OfType<LiteralExpressionSyntax>()
				where lit.Token.Value is string
				select (string)lit.Token.Value).ToArray();
		}
		if (expressionSyntax is ArrayCreationExpressionSyntax arrayCreationExpressionSyntax)
		{
			InitializerExpressionSyntax? initializer = arrayCreationExpressionSyntax.Initializer;
			if (initializer == null)
			{
				return null;
			}
			return (from lit in initializer.Expressions.OfType<LiteralExpressionSyntax>()
				where lit.Token.Value is string
				select (string)lit.Token.Value).ToArray();
		}
		return null;
	}

	private static bool ShouldExclude(string filePath, List<string> excludePatterns)
	{
		if (excludePatterns.Count == 0)
		{
			return false;
		}
		string normalizedPath = filePath.Replace('\\', '/');
		foreach (string excludePattern in excludePatterns)
		{
			string normalizedPattern = excludePattern.Replace('\\', '/');
			if (normalizedPattern.Contains("**"))
			{
				string[] segments = normalizedPattern.Split(["**"], StringSplitOptions.None);
				if (segments.Length == 2)
				{
					string prefixPattern = segments[0].TrimEnd('/');
					string suffixPattern = segments[1].TrimStart('/');
					if ((string.IsNullOrEmpty(prefixPattern) || normalizedPath.Contains(prefixPattern)) && (string.IsNullOrEmpty(suffixPattern) || normalizedPath.Contains(suffixPattern)))
					{
						return true;
					}
				}
			}
			else if (normalizedPath.Contains(normalizedPattern))
			{
				return true;
			}
		}
		return false;
	}

	public void Dispose()
	{
		_workspace?.Dispose();
	}
}
