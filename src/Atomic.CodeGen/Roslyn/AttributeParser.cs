using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Atomic.CodeGen.Roslyn;

public static class AttributeParser
{
	public static Dictionary<string, object?> Parse(AttributeSyntax attribute)
	{
		Dictionary<string, object> dictionary = new Dictionary<string, object>();
		if (attribute.ArgumentList == null)
		{
			return dictionary;
		}
		SeparatedSyntaxList<AttributeArgumentSyntax>.Enumerator enumerator = attribute.ArgumentList.Arguments.GetEnumerator();
		while (enumerator.MoveNext())
		{
			AttributeArgumentSyntax current = enumerator.Current;
			string argumentName = current.NameEquals?.Name.Identifier.Text;
			if (argumentName != null)
			{
				object value = ExtractValue(current.Expression);
				dictionary[argumentName] = value;
			}
		}
		return dictionary;
	}

	private static object? ExtractValue(ExpressionSyntax expression)
	{
		if (!(expression is LiteralExpressionSyntax literal))
		{
			if (!(expression is ImplicitArrayCreationExpressionSyntax arrayExpr))
			{
				if (!(expression is ArrayCreationExpressionSyntax arrayExpr2))
				{
					if (expression is TypeOfExpressionSyntax typeOfExpr)
					{
						return ExtractTypeOfValue(typeOfExpr);
					}
					return null;
				}
				return ExtractExplicitArrayValue(arrayExpr2);
			}
			return ExtractArrayValue(arrayExpr);
		}
		return ExtractLiteralValue(literal);
	}

	private static string? ExtractTypeOfValue(TypeOfExpressionSyntax typeOfExpr)
	{
		return typeOfExpr.Type.ToString();
	}

	private static object? ExtractLiteralValue(LiteralExpressionSyntax literal)
	{
		return literal.Token.Value;
	}

	private static string[]? ExtractArrayValue(ImplicitArrayCreationExpressionSyntax arrayExpr)
	{
		if (arrayExpr.Initializer == null)
		{
			return null;
		}
		return (from e in arrayExpr.Initializer.Expressions.OfType<LiteralExpressionSyntax>()
			select e.Token.ValueText).ToArray();
	}

	private static string[]? ExtractExplicitArrayValue(ArrayCreationExpressionSyntax arrayExpr)
	{
		if (arrayExpr.Initializer == null)
		{
			return null;
		}
		return (from e in arrayExpr.Initializer.Expressions.OfType<LiteralExpressionSyntax>()
			select e.Token.ValueText).ToArray();
	}

	public static string GetString(Dictionary<string, object?> args, string key, string defaultValue = "")
	{
		if (!args.TryGetValue(key, out object value) || !(value is string result))
		{
			return defaultValue;
		}
		return result;
	}

	public static bool GetBool(Dictionary<string, object?> args, string key, bool defaultValue = false)
	{
		if (args.TryGetValue(key, out object value) && value is bool)
		{
			return (bool)value;
		}
		return defaultValue;
	}

	public static string[] GetStringArray(Dictionary<string, object?> args, string key)
	{
		if (!args.TryGetValue(key, out object value) || !(value is string[] result))
		{
			return Array.Empty<string>();
		}
		return result;
	}

	public static string GetTypeName(Dictionary<string, object?> args, string key, string defaultValue = "IEntity")
	{
		if (!args.TryGetValue(key, out object value) || !(value is string result))
		{
			return defaultValue;
		}
		return result;
	}
}
