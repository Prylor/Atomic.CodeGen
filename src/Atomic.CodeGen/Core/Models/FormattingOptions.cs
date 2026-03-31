using System;
using System.Text.Json.Serialization;

namespace Atomic.CodeGen.Core.Models;

public sealed class FormattingOptions
{
	[JsonPropertyName("useTabs")]
	public bool UseTabs { get; set; }

	[JsonPropertyName("indentSize")]
	public int IndentSize { get; set; } = 4;

	[JsonPropertyName("newLine")]
	public string NewLine { get; set; } = Environment.NewLine;

	public string GetIndent()
	{
		if (!UseTabs)
		{
			return new string(' ', IndentSize);
		}
		return "\t";
	}
}
