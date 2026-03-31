using System.Text.Json.Serialization;

namespace Atomic.CodeGen.Core.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AnalyzerMode
{
	Auto,
	MSBuild,
	Buildalyzer
}
