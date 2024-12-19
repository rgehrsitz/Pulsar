# Rules Engine DSL Specification

## Overview

This DSL is a YAML-based format used to define a set of rules for a sensor-driven rules engine. The rules rely on sensor values retrieved from a Redis instance and can include temporal conditions, mathematical expressions, and logical groupings of conditions. They are designed to be human-readable, easily validated, and translatable into optimized C# code for runtime execution.

The DSL is part of a larger system that uses a separate global configuration file to define universal parameters and valid sensors. This separation allows system-wide tooling and services to reference a single source of truth for global configuration, while the rules DSL remains focused solely on rule logic.

## Key Objectives

1. **Human-Readable**:
   The DSL should be easy for engineers to read, write, and maintain.

2. **Separation of Concerns**:
   Global system configuration (including valid sensors) is defined in a separate **system configuration file**. The rules DSL references this global configuration indirectly, enabling different services and tools to validate their I/O against the same source of truth.

3. **Tooling & Validation**:
   A schema (e.g., JSON Schema) will validate both the system configuration and the rules DSL. The rules compiler ensures that all referenced sensors and actions are valid.

4. **Performance & Scalability**:
   The compiled C# code will run efficiently with hundreds to a thousand rules every 100ms. Temporal and mathematical conditions must be performant.

5. **Temporal & Mathematical Conditions**:
   Support conditions based on time durations and basic arithmetic expressions.

6. **Dependency & Ordering**:
   The compiler infers dependencies between rules and sensors, allowing for optimal execution ordering and potential runtime optimizations.

7. **Metadata Generation**:
   The compiler generates metadata for runtime debugging, documentation, and dependency visualization.

8. **Stable Production Deployment**:
   Rules are validated and compiled once, then remain stable unless changes occur during development or bug fixes.

## Configuration Files

### System Configuration File (e.g., `system_config.yaml`)

This file is the system-wide source of truth for global definitions. It is maintained separately from the rules file. Components throughout the system, including the rules compiler, can reference this file to ensure consistency.

**Structure:**

```yaml
version: 1
valid_sensors:
  - temperature
  - humidity
  - pressure
  - alerts:temperature
  - converted_temp
# Additional global configurations can be added here if needed.
```

- version (required): An integer indicating the global configuration schema version.
- valid_sensors (required): A list of strings representing all permissible sensor keys.

System components (e.g., data ingestion, logging services, monitoring tools) and the rules compiler all use this system_config.yaml as a single source of truth for what sensors are valid.

### Rules File (e.g., rules.yaml)

This file contains only the rules. It does not define global keys like valid_sensors. The rules reference sensors and conditions that must be validated against the global configuration during compilation.

Structure:

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

  - name: "ComplexCondition"
    description: "Sets converted_temp when humidity<30 or computed temp>10C"
    conditions:
      any:
        - condition:
            type: comparison
            sensor: humidity
            operator: "<"
            value: 30
        - condition:
            type: expression
            expression: "(temperature - 32) * (5/9) > 10"
    actions:
      - set_value:
          key: "converted_temp"
          value_expression: "(temperature - 32) * (5/9)"
      - send_message:
          channel: "conversion"
          message: "Temperature converted and above threshold!"
```

### Validation & Compilation Flow

1. System Configuration Load:
   The compiler and other services first load system_config.yaml. From here, they obtain the valid_sensors and other global configurations.

2. Rules Load & Validation:
   The compiler then loads rules.yaml.

- The DSL schema ensures structural correctness.
- The compiler checks each referenced sensor against valid_sensors from the system configuration.
- The compiler validates arithmetic expressions and condition types.

3. Compilation:
   On successful validation, the compiler:

- Infers dependencies and optimizes the order of rule execution.
- Generates multiple C# files (e.g., 100 rules per file) for manageability.
- Integrates temporal conditions by generating code that references runtime-managed state.
- Produces a metadata class describing all rules, their sensors, and actions.

### Rule Structure

Each rule in rules.yaml must have:

- name (required): Unique identifier for the rule.
- description (optional): A human-readable explanation.
- conditions (required): Logical constructs using all, any, and condition.
- actions (required): A list of actions to execute if conditions are met.

### Conditions

Logical Grouping:

- all: All conditions in its list must be true.
- any: At least one condition in its list must be true.
  Condition Types:

1. Comparison:

```yaml
condition:
  type: comparison
  sensor: <sensor_key>
  operator: "<|>|<=|>=|==|!="
  value: <number>
```

2. Threshold Over Time:

```yaml
condition:
  type: threshold_over_time
  sensor: <sensor_key>
  threshold: <number>
  duration: <time_span> (e.g., 500ms, 2s)
```

3. Expression:

```yaml
condition:
  type: expression
  expression: "(<sensor_key> [+-*/] <number> ...) <operator> <number>"
```

### Actions

1. Set Value:

```yaml
- set_value:
    key: <string>
    value: <string> | value_expression: <expression_string>
```

2. Send Message:

```yaml
- send_message:
    channel: <string>
    message: <string>
```

### Metadata Generation

A separate generated file (e.g., RuleMetadata.cs) will provide a dictionary of all rules and their characteristics. This includes:

- Rule name
- Description
- Sensors referenced
- Actions performed
- Condition types

This metadata aids in debugging, visualization, and documentation.

### Error Handling & Logging

- Compile-Time Errors:
  - If a rule references an invalid sensor, compilation fails.
  - If an expression is malformed, compilation fails.
  - Descriptive error messages help identify and fix issues quickly.
- Runtime Logging:
  - Optional hooks in generated code for logging rule evaluations, condition checks, and actions.

### Lifecycle

- Development:
  - Engineers write and edit system_config.yaml (for global sensors) and rules.yaml (for logic).
  - The code is validated, compiled, and tested before deployment.
- Deployment:
  - Once deployed, the rules remain stable. Any changes require re-validation and compilation.

---

This Updated DSL Specification serves as the authoritative reference for the rules DSL, the system configuration, and how they interact. It defines how to separate global configuration from rule logic, how to validate and compile the rules, and how to generate the necessary runtime artifacts (C# code and metadata).
