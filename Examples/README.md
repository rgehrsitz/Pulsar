# Pulsar/Beacon Examples

This directory contains example rule definitions, configurations, and scripts for the Pulsar/Beacon project. These examples demonstrate how to define rules, configure the system, and run the Pulsar compiler to generate Beacon applications.

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

Use the Pulsar compiler to generate a Beacon application from the example rules and configuration:

```bash
# Navigate to the Pulsar root directory
cd /path/to/Pulsar

# Run the compiler with example files
dotnet run --project Pulsar.Compiler -- beacon \
  --rules=Examples/BasicRules/temperature_rules.yaml \
  --config=Examples/BasicRules/system_config.yaml \
  --output=MyBeacon
```

This will generate a complete Beacon application in the `MyBeacon` directory.

### 4. Build and Run the Generated Application

```bash
# Navigate to the generated Beacon directory
cd MyBeacon/Beacon

# Build the solution
dotnet build

# Run the application
dotnet run --project Beacon.Runtime
```

### 5. Test with the Provided Script

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

## Output Directories

When you run the Pulsar compiler, it generates output in the specified directory. These output directories are excluded from version control by default. If you want to preserve generated code, you should copy it to a different location or modify the `.gitignore` file.
