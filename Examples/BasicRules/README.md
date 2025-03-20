# Basic Temperature Rules Example

This directory contains a basic example of temperature monitoring rules for the Pulsar/Beacon project. It demonstrates how to define simple rules for temperature monitoring and how to configure the system.

## Files

- `temperature_rules.yaml` - Example rule definitions for temperature monitoring
- `system_config.yaml` - Full system configuration example
- `reference_config.yaml` - Minimal reference configuration
- `test_run.sh` - Script demonstrating how to run Pulsar with different configurations

## Rule Definitions

The `temperature_rules.yaml` file contains two example rules:

1. **HighTemperatureRule** - Detects when temperature exceeds a threshold and sets an alert flag
2. **HeatIndexCalculationRule** - Calculates heat index from temperature and humidity

## Configuration Files

- `system_config.yaml` - A complete configuration with Redis settings, valid sensors, cycle time, and buffer capacity
- `reference_config.yaml` - A minimal configuration showing only required settings

## Running the Example

Use the `test_run.sh` script to run the Pulsar compiler with different configurations:

```bash
# Make the script executable if needed
chmod +x test_run.sh

# Run the test script
./test_run.sh
```

This script will:
1. Clean the output directory
2. Run the test command with the system configuration
3. Run the beacon generation command with the reference configuration
4. Run the beacon generation command with the full system configuration

## Output

The generated output will be placed in the `output` directory, which is excluded from version control. The output includes:

- Generated Beacon solution
- Interface definitions
- Runtime components
- Test project

## Modifying the Example

To modify this example:

1. Edit the `temperature_rules.yaml` file to change the rules
2. Edit the configuration files to change the system settings
3. Run the `test_run.sh` script to generate new output

Refer to the [Rules Engine documentation](../../docs/Rules-Engine.md) for detailed information on rule syntax and capabilities.
