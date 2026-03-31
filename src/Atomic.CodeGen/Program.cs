using System.CommandLine;
using System.Threading.Tasks;
using Atomic.CodeGen.Commands;

namespace Atomic.CodeGen;

internal class Program
{
	private static async Task<int> Main(string[] args)
	{
		RootCommand rootCommand = new RootCommand("Atomic Entity API Code Generator");
		rootCommand.Add(WizardCommand.Create());
		rootCommand.Add(InitCommand.Create());
		rootCommand.Add(ConfigureCommand.Create());
		rootCommand.Add(GenerateCommand.Create());
		rootCommand.Add(ScanCommand.Create());
		rootCommand.Add(ScanDomainsCommand.Create());
		rootCommand.Add(RenameCommand.Create());
		rootCommand.Add(RenameAtCommand.Create());
		rootCommand.Add(IdeCommand.Create());
		rootCommand.Description = "\r\n╔═══════════════════════════════════════════════════════════════╗\r\n║                Atomic Entity API Code Generator               ║\r\n║                                                               ║\r\n║  Generates extension methods from C# [EntityAPI] attributes   ║\r\n╚═══════════════════════════════════════════════════════════════╝\r\n\r\nUsage:\r\n  atomic-codegen wizard            Complete setup wizard (recommended for new users)\r\n  atomic-codegen init              Initialize configuration (interactive)\r\n  atomic-codegen configure         View and modify configuration\r\n  atomic-codegen generate          Generate API files once\r\n  atomic-codegen scan              Scan for definitions (dry run)\r\n  atomic-codegen rename            Rename symbols (interactive/direct)\r\n  atomic-codegen rename-at         Rename at cursor (IDE integration)\r\n  atomic-codegen ide               Setup IDE integration (Rider)\r\n\r\nOptions:\r\n  -p, --project <path>             Unity project root (default: current directory)\r\n  -v, --verbose                    Enable verbose logging\r\n  -h, --help                       Show help information\r\n        ";
		return await rootCommand.InvokeAsync(args);
	}
}
