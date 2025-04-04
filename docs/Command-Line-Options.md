# Pulsar Command Line Options

This document provides a comprehensive guide to the command line options for the Pulsar Compiler tool.

## Basic Usage

```bash
dotnet run --project Pulsar.Compiler -- <command> [options]
```

Or if using a standalone published version:

```bash
Pulsar.Compiler <command> [options]
```

## Commands

Pulsar.Compiler supports the following main commands:

### beacon

Generates an AOT-compatible Beacon solution from rules.

```bash
dotnet run --project Pulsar.Compiler -- beacon --rules=<rules-path> --config=<config-path> --output=<output-path> [--target=<runtime-id>] [--verbose]
```

**Options:**
- `--rules <path>`: Path to YAML rule file or directory containing rule files (required)
- `--config <path>`: Path to system configuration YAML file (default: system_config.yaml)
- `--output <path>`: Output directory for the Beacon solution (default: current directory)
- `--target <runtime>`: Target runtime identifier for AOT compilation (default: win-x64)
- `--verbose`: Enable verbose logging

**Example:**
```bash
dotnet run --project Pulsar.Compiler -- beacon --rules=Examples/BasicRules/temperature_rules.yaml --config=Examples/BasicRules/system_config.yaml --output=MyBeacon
```

### test

Tests Pulsar rules and configuration files.

```bash
dotnet run --project Pulsar.Compiler -- test --rules=<rules-path> --config=<config-path> --output=<output-dir> [--clean=true|false]
```

**Options:**
- `--rules <path>`: Path to rules file (required)
- `--config <path>`: Path to config file (required)
- `--output <path>`: Output directory (default: temporary directory)
- `--clean`: Whether to clean the output directory first (default: true)

### compile

Compiles rules into a deployable project.

```bash
dotnet run --project Pulsar.Compiler -- compile --rules=<rules-path> --output=<output-path> [--config=<config-path>] [--target=<runtime-id>] [--aot] [--debug]
```

**Options:**
- `--rules <path>`: Path to YAML rule file (required)
- `--output <path>`: Output directory (required)
- `--config <path>`: System configuration file (default: system_config.yaml)
- `--target <id>`: Target runtime identifier (e.g., win-x64, linux-x64)
- `--aot`: Enable AOT-compatible code generation
- `--debug`: Include debug symbols and enhanced logging
- `--max-rules <num>`: Maximum rules per file (default: 100)
- `--complexity-threshold <num>`: Complexity threshold for splitting rules (default: 100)
- `--parallel`: Group parallel rules together

### validate

Validates rules without generating code.

```bash
dotnet run --project Pulsar.Compiler -- validate --rules=<rules-path> [--config=<config-path>]
```

**Options:**
- `--rules <path>`: Path to YAML rule file (required)
- `--config <path>`: System configuration file (optional)

### init

Initializes a new project with example files.

```bash
dotnet run --project Pulsar.Compiler -- init --output=<output-path>
```

**Options:**
- `--output <path>`: Output directory (default: current directory)

### generate

Generates a buildable project (similar to beacon but with different defaults).

```bash
dotnet run --project Pulsar.Compiler -- generate --rules=<rules-path> --output=<output-path> [--config=<config-path>]
```

**Options:**
- Similar to the compile command

## Common Options

- `--verbose`: Enable verbose logging (supported by most commands)
- `--target`: Runtime identifier for AOT compilation:
  - Valid values: `win-x64`, `linux-x64`, `osx-x64`
  - Default: `win-x64`
- `--config`: Path to system configuration YAML file

## Examples

### Basic Beacon Generation

```bash
dotnet run --project Pulsar.Compiler -- beacon --rules=my-rules.yaml --config=system_config.yaml --output=MyBeacon
```

### Testing Rules

```bash
dotnet run --project Pulsar.Compiler -- test --rules=rules.yaml --config=system_config.yaml --output=test-output
```

### Initializing a New Project

```bash
dotnet run --project Pulsar.Compiler -- init --output=new-project
```

## Building and Publishing Pulsar

You can build the Pulsar.Compiler project using the following commands:

```bash
dotnet build Pulsar.Compiler/Pulsar.Compiler.csproj
```

To create a publishable version:

```bash
dotnet publish Pulsar.Compiler/Pulsar.Compiler.csproj
```