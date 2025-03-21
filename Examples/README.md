# Pulsar/Beacon Examples

This directory contains example rule definitions, configurations, and scripts for the Pulsar/Beacon project. These examples demonstrate how to define rules, configure the system, and run the Pulsar compiler to generate AOT-compatible Beacon applications using the template-based code generation approach. All examples follow best practices for rule definition and system configuration.

## Directory Structure

- **BasicRules/** - Basic example of temperature monitoring rules
  - `temperature_rules.yaml` - Example rule definitions for temperature monitoring
  - `system_config.yaml` - Full system configuration example
  - `reference_config.yaml` - Minimal reference configuration
  - `test_run.sh` - Script demonstrating how to run Pulsar with different configurations

## How to Use These Examples

### 1. Examine the Rule Definitions

The `temperature_rules.yaml` file contains example rule definitions that demonstrate how to create conditions and actions. Review this file to understand the rule syntax and structure.

```yaml
# Example rule definition
rules:
  - name: HighTemperatureRule
    description: Detects when temperature exceeds threshold and sets alert flag
    conditions:
      all:
        - condition:
            type: comparison
            sensor: input:temperature
            operator: '>'
            value: 30
    actions:
      - set_value:
          key: output:high_temperature_alert
          value_expression: 'true'
```

### 2. Review the Configuration Files

Two configuration files are provided:
- `system_config.yaml` - A complete configuration with all available options
- `reference_config.yaml` - A minimal configuration showing only required settings

### 3. Generate a Beacon Application

Use the Pulsar compiler to generate a Beacon application from the example rules and configuration. The compiler uses templates from the `Pulsar.Compiler/Config/Templates` directory to generate a complete, AOT-compatible application:

```bash
# Navigate to the Pulsar root directory
cd /path/to/Pulsar

# Run the compiler with example files
dotnet run --project Pulsar.Compiler -- beacon \
  --rules=Examples/BasicRules/temperature_rules.yaml \
  --config=Examples/BasicRules/system_config.yaml \
  --output=MyBeacon
```

This will generate a complete, standalone Beacon application in the `MyBeacon` directory. The generated code is fully AOT-compatible and can be deployed in environments without JIT compilation.

### 4. Build and Run the Generated Application

```bash
# Navigate to the generated Beacon directory
cd MyBeacon/Beacon

# Build the solution
dotnet build

# Run the application
dotnet run --project Beacon.Runtime
```

### 5. Publish as AOT-Compatible Executable

To create a standalone, AOT-compatible executable for deployment:

```bash
# Navigate to the generated Beacon directory
cd MyBeacon/Beacon

# For Linux x64
dotnet publish Beacon.Runtime -c Release -r linux-x64 --self-contained true -p:PublishAot=true

# For Windows x64
dotnet publish Beacon.Runtime -c Release -r win-x64 --self-contained true -p:PublishAot=true

# For macOS x64
dotnet publish Beacon.Runtime -c Release -r osx-x64 --self-contained true -p:PublishAot=true
```

The published executable will be in the `Beacon.Runtime/bin/Release/net8.0/<runtime>/publish/` directory.

### 6. Test with the Provided Script

The `test_run.sh` script demonstrates how to run the Pulsar compiler with different configurations:

```bash
# Navigate to the BasicRules directory
cd Examples/BasicRules

# Run the test script
./test_run.sh
```

This script will generate output in the `output` directory, which is excluded from version control.

## Creating Your Own Rules

To create your own rules:

1. Create a new YAML file for your rule definitions
2. Create a configuration file or use one of the example configurations
3. Run the Pulsar compiler with your files
4. Build and run the generated Beacon application

Refer to the [Rules Engine documentation](../docs/Rules-Engine.md) for detailed information on rule syntax and capabilities.

## Version Control Considerations

### Output Directories

When you run the Pulsar compiler, it generates output in the specified directory. These output directories are **excluded from version control by default** via the `.gitignore` file. This is intentional and follows best practices for generated code.

### Best Practices

1. **Only commit source files** (YAML rule definitions, configuration files, scripts)
2. **Never commit generated code** to version control
3. **Regenerate code** during build or deployment processes
4. **Document the generation process** so others can reproduce it

This approach ensures clean version control history and prevents conflicts with generated files. If you need to preserve specific generated code for reference, consider creating a separate repository or documentation for that purpose.
