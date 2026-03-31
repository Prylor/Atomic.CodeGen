using System;

namespace Atomic.CodeGen.Utils;

public static class Logger
{
	private static bool _verbose;

	public static void SetVerbose(bool verbose)
	{
		_verbose = verbose;
	}

	public static void LogInfo(string message)
	{
		Console.ForegroundColor = ConsoleColor.White;
		Console.WriteLine("[INFO] " + message);
		Console.ResetColor();
	}

	public static void LogSuccess(string message)
	{
		Console.ForegroundColor = ConsoleColor.Green;
		Console.WriteLine("✓ " + message);
		Console.ResetColor();
	}

	public static void LogWarning(string message)
	{
		Console.ForegroundColor = ConsoleColor.Yellow;
		Console.WriteLine("⚠ " + message);
		Console.ResetColor();
	}

	public static void LogError(string message)
	{
		Console.ForegroundColor = ConsoleColor.Red;
		Console.Error.WriteLine("✗ " + message);
		Console.ResetColor();
	}

	public static void LogVerbose(string message)
	{
		if (_verbose)
		{
			Console.ForegroundColor = ConsoleColor.Gray;
			Console.WriteLine("[VERBOSE] " + message);
			Console.ResetColor();
		}
	}

	public static void LogHeader(string message)
	{
		Console.ForegroundColor = ConsoleColor.Cyan;
		Console.WriteLine();
		Console.WriteLine("═══════════════════════════════════════════════════════");
		Console.WriteLine("  " + message);
		Console.WriteLine("═══════════════════════════════════════════════════════");
		Console.ResetColor();
	}
}
