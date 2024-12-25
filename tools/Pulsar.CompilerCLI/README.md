# Pulsar Rule Compiler CLI

A command-line tool for compiling Pulsar rule definitions into executable C# code.

## Usage

```bash
PulsarCompilerCLI --config <config-file> --rules <rules-file> --output <output-dir> [options]
```

### Required Arguments

- `-c, --config`: Path to the system configuration YAML file
- `-r, --rules`: Path to the rules YAML file
- `-o, --output`: Output directory for compiled rules

### Optional Arguments

- `-n, --namespace`: Namespace for generated code (default: "Pulsar.CompiledRules")
- `-v, --verbose`: Enable verbose logging

### Example

```bash
PulsarCompilerCLI -c system_config.yaml -r rules.yaml -o ./output -v
```

## Output Files

The compiler generates two files:

1. `CompiledRules.cs`: The compiled C# code containing the rule implementations
2. `RuleMetadata.json`: Metadata about the compiled rules for debugging and documentation

## Exit Codes

- 0: Success
- 1: Error (invalid input, compilation failure, etc.)
