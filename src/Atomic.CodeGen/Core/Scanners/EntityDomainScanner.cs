using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Atomic.CodeGen.Core.Models;
using Atomic.CodeGen.Core.Models.EntityDomain;
using Atomic.CodeGen.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Atomic.CodeGen.Core.Scanners;

public static class EntityDomainScanner
{
	public static async Task<Dictionary<string, EntityDomainDefinition>> ScanAsync(CodeGenConfig config)
	{
		Dictionary<string, EntityDomainDefinition> results = new Dictionary<string, EntityDomainDefinition>();
		List<string> scannedFiles = new FileScanner(config).Scan();
		foreach (string filePath in scannedFiles)
		{
			EntityDomainDefinition entityDomainDefinition = await ParseFileAsync(filePath, config);
			if (entityDomainDefinition != null)
			{
				results[filePath] = entityDomainDefinition;
			}
		}
		return results;
	}

	public static async Task<EntityDomainDefinition?> ParseFileAsync(string filePath, CodeGenConfig config)
	{
		_ = 2;
		try
		{
			if (!(await CSharpSyntaxTree.ParseText(await File.ReadAllTextAsync(filePath)).GetRootAsync() is CompilationUnitSyntax syntaxRoot))
			{
				return null;
			}
			ClassDeclarationSyntax domainClass = syntaxRoot.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault((ClassDeclarationSyntax c) => ImplementsInterface(c, "IEntityDomain"));
			if (domainClass != null)
			{
				Logger.LogVerbose("Found IEntityDomain implementation: " + domainClass.Identifier.Text + " in " + filePath);
				return ParseDomainClass(domainClass, filePath, syntaxRoot, config);
			}
			ClassDeclarationSyntax builderClass = syntaxRoot.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault((ClassDeclarationSyntax c) => InheritsFrom(c, "EntityDomainBuilder"));
			if (builderClass != null)
			{
				Logger.LogVerbose("Found EntityDomainBuilder subclass: " + builderClass.Identifier.Text + " in " + filePath);
				return await ParseBuilderClassAsync(builderClass, filePath, syntaxRoot, config);
			}
			return null;
		}
		catch (Exception ex)
		{
			Logger.LogError("Failed to parse " + filePath + ": " + ex.Message);
			return null;
		}
	}

	private static bool ImplementsInterface(ClassDeclarationSyntax classDecl, string interfaceName)
	{
		if (classDecl.BaseList == null)
		{
			return false;
		}
		return classDecl.BaseList.Types.Select((BaseTypeSyntax t) => t.Type).OfType<IdentifierNameSyntax>().Any((IdentifierNameSyntax id) => id.Identifier.Text == interfaceName);
	}

	private static bool InheritsFrom(ClassDeclarationSyntax classDecl, string baseClassName)
	{
		if (classDecl.BaseList == null)
		{
			return false;
		}
		return classDecl.BaseList.Types.Select((BaseTypeSyntax t) => t.Type).OfType<IdentifierNameSyntax>().Any((IdentifierNameSyntax id) => id.Identifier.Text == baseClassName);
	}

	private static Task<EntityDomainDefinition?> ParseBuilderClassAsync(ClassDeclarationSyntax classDecl, string sourceFile, CompilationUnitSyntax root, CodeGenConfig config)
	{
		try
		{
			EntityDomainDefinition definition = new EntityDomainDefinition
			{
				SourceFile = sourceFile,
				ClassName = classDecl.Identifier.Text
			};
			definition.DetectedImports = (from u in root.Usings
				select u.Name?.ToString() into name
				where !string.IsNullOrWhiteSpace(name)
				select (name)).ToList();
			definition.EntityName = GetPropertyValue(classDecl, "EntityName") ?? throw new Exception("EntityName is required in " + classDecl.Identifier.Text);
			definition.Namespace = GetPropertyValue(classDecl, "Namespace") ?? throw new Exception("Namespace is required in " + classDecl.Identifier.Text);
			definition.Directory = GetPropertyValue(classDecl, "Directory") ?? ("Assets/Scripts/Generated/" + definition.EntityName);
			MethodDeclarationSyntax configureMethod = classDecl.Members.OfType<MethodDeclarationSyntax>().FirstOrDefault((MethodDeclarationSyntax m) => m.Identifier.Text == "Configure");
			if (configureMethod == null || configureMethod.Body == null)
			{
				Logger.LogWarning("Configure() method not found or empty in " + classDecl.Identifier.Text);
				return Task.FromResult(definition);
			}
			if (ParseConfigureMethodCalls(configureMethod.Body, definition))
			{
				return Task.FromResult<EntityDomainDefinition>(null);
			}
			return Task.FromResult(definition);
		}
		catch (Exception ex)
		{
			Logger.LogError("Failed to parse builder class " + classDecl.Identifier.Text + ": " + ex.Message);
			return Task.FromResult<EntityDomainDefinition>(null);
		}
	}

	private static bool ParseConfigureMethodCalls(BlockSyntax methodBody, EntityDomainDefinition definition)
	{
		IEnumerable<InvocationExpressionSyntax> invocations = methodBody.DescendantNodes().OfType<InvocationExpressionSyntax>();
		int modeCallCount = 0;
		bool hasError = false;
		foreach (InvocationExpressionSyntax invocation in invocations)
		{
			switch (GetMethodName(invocation))
			{
			case "EntityMode":
				definition.Mode = EntityMode.Entity;
				modeCallCount++;
				break;
			case "EntitySingletonMode":
				definition.Mode = EntityMode.EntitySingleton;
				modeCallCount++;
				break;
			case "SceneEntityMode":
				definition.Mode = EntityMode.SceneEntity;
				modeCallCount++;
				break;
			case "SceneEntitySingletonMode":
				definition.Mode = EntityMode.SceneEntitySingleton;
				modeCallCount++;
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
		if (modeCallCount == 0)
		{
			Logger.LogWarning("No entity mode specified in " + definition.ClassName + ". Defaulting to EntityMode.");
			definition.Mode = EntityMode.Entity;
		}
		else if (modeCallCount > 1)
		{
			Logger.LogError("Multiple entity modes specified in " + definition.ClassName + ". Choose only ONE: EntityMode(), EntitySingletonMode(), SceneEntityMode(), or SceneEntitySingletonMode()");
			hasError = true;
		}
		return hasError;
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

	private static EntityDomainDefinition ParseDomainClass(ClassDeclarationSyntax classDecl, string sourceFile, CompilationUnitSyntax root, CodeGenConfig config)
	{
		EntityDomainDefinition definition = new EntityDomainDefinition
		{
			SourceFile = sourceFile,
			ClassName = classDecl.Identifier.Text
		};
		definition.DetectedImports = (from u in root.Usings
			select u.Name?.ToString() into name
			where !string.IsNullOrWhiteSpace(name)
			select (name)).ToList();
		definition.EntityName = GetPropertyValue(classDecl, "EntityName") ?? throw new Exception("EntityName is required in " + classDecl.Identifier.Text);
		definition.Namespace = GetPropertyValue(classDecl, "Namespace") ?? throw new Exception("Namespace is required in " + classDecl.Identifier.Text);
		definition.Directory = GetPropertyValue(classDecl, "Directory") ?? ("Assets/Scripts/Generated/" + definition.EntityName);
		definition.Mode = GetEnumPropertyValue<EntityMode>(classDecl, "Mode").GetValueOrDefault();
		bool? proxyOverride = GetBoolPropertyValue(classDecl, "GenerateProxy");
		bool shouldGenerateProxy;
		if (proxyOverride.HasValue)
		{
			shouldGenerateProxy = proxyOverride == true;
		}
		else
		{
			EntityMode mode = definition.Mode;
			bool isSceneMode = mode == EntityMode.SceneEntity || mode == EntityMode.SceneEntitySingleton;
			shouldGenerateProxy = isSceneMode;
		}
		definition.GenerateProxy = shouldGenerateProxy;
		bool? worldOverride = GetBoolPropertyValue(classDecl, "GenerateWorld");
		bool shouldGenerateWorld;
		if (worldOverride.HasValue)
		{
			shouldGenerateWorld = worldOverride == true;
		}
		else
		{
			EntityMode mode = definition.Mode;
			bool isSceneMode = mode == EntityMode.SceneEntity || mode == EntityMode.SceneEntitySingleton;
			shouldGenerateWorld = isSceneMode;
		}
		definition.GenerateWorld = shouldGenerateWorld;
		definition.Installers = GetEnumPropertyValue<EntityInstallerMode>(classDecl, "Installers").GetValueOrDefault();
		definition.Aspects = GetEnumPropertyValue<EntityAspectMode>(classDecl, "Aspects").GetValueOrDefault();
		definition.Pools = GetEnumPropertyValue<EntityPoolMode>(classDecl, "Pools").GetValueOrDefault();
		definition.Factories = GetEnumPropertyValue<EntityFactoryMode>(classDecl, "Factories").GetValueOrDefault();
		definition.Bakers = GetEnumPropertyValue<EntityBakerMode>(classDecl, "Bakers").GetValueOrDefault();
		definition.Views = GetEnumPropertyValue<EntityViewMode>(classDecl, "Views").GetValueOrDefault();
		definition.ExcludeImports = GetStringArrayPropertyValue(classDecl, "ExcludeImports");
		definition.TargetProject = GetPropertyValue(classDecl, "TargetProject");
		return definition;
	}

	private static string? GetPropertyValue(ClassDeclarationSyntax classDecl, string propertyName)
	{
		PropertyDeclarationSyntax propertyDeclarationSyntax = classDecl.Members.OfType<PropertyDeclarationSyntax>().FirstOrDefault((PropertyDeclarationSyntax p) => p.Identifier.Text == propertyName);
		if (propertyDeclarationSyntax == null)
		{
			return null;
		}
		object obj = propertyDeclarationSyntax.ExpressionBody?.Expression;
		if (obj == null)
		{
			AccessorListSyntax? accessorList = propertyDeclarationSyntax.AccessorList;
			obj = ((accessorList == null) ? null : accessorList.Accessors.FirstOrDefault((AccessorDeclarationSyntax a) => a.Kind() == SyntaxKind.GetAccessorDeclaration)?.ExpressionBody?.Expression);
		}
		ExpressionSyntax expressionSyntax = (ExpressionSyntax)obj;
		if (expressionSyntax == null)
		{
			return null;
		}
		if (expressionSyntax is LiteralExpressionSyntax { Token: { Value: string value } })
		{
			return value;
		}
		_ = expressionSyntax is InterpolatedStringExpressionSyntax;
		return null;
	}

	private static T? GetEnumPropertyValue<T>(ClassDeclarationSyntax classDecl, string propertyName) where T : struct, Enum
	{
		PropertyDeclarationSyntax propertyDeclarationSyntax = classDecl.Members.OfType<PropertyDeclarationSyntax>().FirstOrDefault((PropertyDeclarationSyntax p) => p.Identifier.Text == propertyName);
		if (propertyDeclarationSyntax == null)
		{
			return null;
		}
		object obj = propertyDeclarationSyntax.ExpressionBody?.Expression;
		if (obj == null)
		{
			AccessorListSyntax? accessorList = propertyDeclarationSyntax.AccessorList;
			obj = ((accessorList == null) ? null : accessorList.Accessors.FirstOrDefault((AccessorDeclarationSyntax a) => a.Kind() == SyntaxKind.GetAccessorDeclaration)?.ExpressionBody?.Expression);
		}
		ExpressionSyntax expressionSyntax = (ExpressionSyntax)obj;
		if (expressionSyntax == null)
		{
			return null;
		}
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
		object obj = propertyDeclarationSyntax.ExpressionBody?.Expression;
		if (obj == null)
		{
			AccessorListSyntax? accessorList = propertyDeclarationSyntax.AccessorList;
			obj = ((accessorList == null) ? null : accessorList.Accessors.FirstOrDefault((AccessorDeclarationSyntax a) => a.Kind() == SyntaxKind.GetAccessorDeclaration)?.ExpressionBody?.Expression);
		}
		ExpressionSyntax expressionSyntax = (ExpressionSyntax)obj;
		if (expressionSyntax == null)
		{
			return null;
		}
		if (expressionSyntax is LiteralExpressionSyntax node)
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
		object obj = propertyDeclarationSyntax.ExpressionBody?.Expression;
		if (obj == null)
		{
			AccessorListSyntax? accessorList = propertyDeclarationSyntax.AccessorList;
			obj = ((accessorList == null) ? null : accessorList.Accessors.FirstOrDefault((AccessorDeclarationSyntax a) => a.Kind() == SyntaxKind.GetAccessorDeclaration)?.ExpressionBody?.Expression);
		}
		ExpressionSyntax expressionSyntax = (ExpressionSyntax)obj;
		if (expressionSyntax == null)
		{
			return null;
		}
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
}
