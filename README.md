# Pulsar Rules Engine

A high-performance rules engine for processing sensor data with compiled rules. Pulsar is designed to efficiently evaluate hundreds to thousands of rules against sensor data stored in Redis, with support for temporal conditions and mathematical expressions.

## Key Features

- YAML-based rule definitions for easy authoring and maintenance
- Build-time compilation of rules to optimized C# code
- Support for temporal conditions and mathematical expressions
- Dependency analysis and automatic rule ordering
- Redis integration for sensor data storage
- High-performance execution with minimal overhead
- Designed for 100ms evaluation cycles

## Project Structure

```
src/
├── Pulsar.Core/           # Core types and interfaces
├── Pulsar.RuleDefinition/ # Rule parsing and validation 
├── Pulsar.Models/         # Data models and compiled rule structures
├── Pulsar.Compiler/       # Rule compilation and code generation
└── Pulsar.Runtime/        # Runtime execution engine and Redis integration

tests/
└── Pulsar.RuleDefinition.Tests/ # Unit tests

examples/
├── CompiledRules/         # Sample compiled rule output
└── RulesTest/            # Example rule definitions and tests
```

## Getting Started

### Prerequisites

- .NET 9.0 or higher
- Redis server instance
- Visual Studio 2022 or later (recommended)

### Building the Project

```bash
dotnet build
```

### Running Tests

```bash
dotnet test
```

## Rule Definition Example

Rules are defined in YAML format:

```yaml
rules:
  - name: "HighTemperatureAlert"
    description: "Alerts when temperature is >50 for 500ms"
    conditions:
      all:
        - condition:
            type: threshold_over_time
            sensor: temperature
            threshold: 50
            duration: 500ms
    actions:
      - set_value:
          key: "alerts:temperature"
          value: "1"
      - send_message:
          channel: "alerts"
          message: "Temperature too high!"
```

## System Configuration

Global configuration including valid sensors is defined in a separate `system_config.yaml`:

```yaml
version: 1
valid_sensors:
  - temperature
  - humidity
  - pressure
  - alerts:temperature
  - converted_temp
```

## Architecture

1. **Rule Definition**: Rules are authored in YAML with support for:
   - Comparison conditions
   - Threshold over time conditions
   - Mathematical expressions
   - Multiple action types

2. **Compilation**: The compiler:
   - Validates rule syntax and references
   - Analyzes dependencies
   - Generates optimized C# code
   - Produces execution metadata

3. **Runtime**: The engine:
   - Polls Redis for sensor data
   - Evaluates compiled rules
   - Executes actions
   - Maintains temporal state when needed

## Current Status

The system currently supports:
- [x] YAML rule parsing
- [x] Rule compilation to C# code
- [x] Redis integration
- [x] Basic rule execution
- [ ] Full temporal conditions
- [ ] Complete action handlers
- [ ] Production deployment tooling

## Contributing

Please see our contribution guidelines (coming soon).

## License

This project is licensed under the MIT License - see the LICENSE file for details.

