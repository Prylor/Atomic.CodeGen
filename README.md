# Atomic.CodeGen

Code generator for **Atomic Entity Framework** — generates extension methods from C# `[EntityAPI]` attribute definitions.

Features: auto-project detection, orphaned file cleanup, cross-platform support.

[![NuGet](https://img.shields.io/nuget/v/Atomic.CodeGen)](https://www.nuget.org/packages/Atomic.CodeGen/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

## Installation

```bash
dotnet tool install -g Atomic.CodeGen
```

## Usage

```
atomic-codegen wizard            Complete setup wizard (recommended for new users)
atomic-codegen init              Initialize configuration (interactive)
atomic-codegen configure         View and modify configuration
atomic-codegen generate          Generate API files once
atomic-codegen scan              Scan for definitions (dry run)
atomic-codegen scan-domains      Scan for entity domain definitions
atomic-codegen rename            Rename symbols (interactive/direct)
atomic-codegen rename-at         Rename at cursor (IDE integration)
atomic-codegen ide               Setup IDE integration (Rider)
```

### Options

| Option | Description |
|--------|-------------|
| `-p, --project <path>` | Unity project root (default: current directory) |
| `-v, --verbose` | Enable verbose logging |
| `-h, --help` | Show help information |

## Quick Start

1. Install the tool globally
2. Navigate to your Unity project root
3. Run `atomic-codegen wizard` for guided setup
4. Run `atomic-codegen generate` to generate code

## Building from Source

```bash
dotnet build
```


## License

[MIT](LICENSE)
