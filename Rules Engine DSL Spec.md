# Rules Engine DSL Specification

## Overview

This DSL is a YAML-based format that describes a set of rules for a sensor-based rules engine. The rules rely on sensor values retrieved from a Redis instance and can include temporal conditions, mathematical expressions, and logical grouping of conditions. The DSL is intended to be human-readable, easily validated, and transformable into optimized C# code for runtime execution. Additionally, the DSL design supports generating metadata to assist in debugging, dependency visualization, and documentation.

## Key Objectives

1. **Human-Readable**: The DSL should be intuitive and easy to maintain by engineers.
2. **Tooling & Validation**: A schema (JSON Schema or similar) will validate rule structure and references before compilation.
3. **Performance & Scalability**: The compiler will generate optimized C# code, suitable for hundreds to a thousand rules, executing every 100ms.
4. **Temporal & Mathematical Conditions**: Support conditions based on time durations and simple arithmetic expressions.
5. **Dependency & Ordering**: The compiler infers dependencies between rules and sensors, allowing for an optimal execution order and potential runtime optimizations.
6. **Metadata Generation**: The compiler generates metadata to provide insights for runtime debugging, documentation, and tooling.
7. **Stability in Production**: Rules are validated and compiled once. No changes are expected at runtime after deployment.

## Document Structure

The DSL file (e.g., `rules.yaml`) will include:

- A version number for schema evolution.
- A list of valid data sources.
- A collection of rules, each with conditions and actions.

### Top-Level Keys

- **`version` (required)**: An integer indicating the DSL schema version. Example: `version: 1`
- **`valid_data_sources` (required)**: A list of strings representing all permissible data keys.
  Example:

          valid_data_sources:
            - temperature
            - humidity
            - pressure
            - alerts:temperature
            - converted_temp

- **`rules` (required)**: A list of rule objects.

## Rule Structure

Each rule entry in `rules` must contain:

- **`name` (required)**: A unique identifier for the rule.
- **`description` (optional)**: A human-readable explanation of what the rule does.
- **`conditions` (required)**: A logical structure defining when the rule fires.
- **`actions` (required)**: A set of instructions that are executed if the rule’s conditions are met.

Example:

    rules:
      - name: "HighTemperatureAlert"
        description: "Alerts when temperature is >50 for 500ms"
        conditions:
          all:
            - condition:
                type: threshold_over_time
                data_source: temperature
                threshold: 50
                duration: 500ms
        actions:
          - set_value:
              key: "alerts:temperature"
              value: "1"
          - send_message:
              channel: "alerts"
              message: "Temperature too high!"

## Conditions

Conditions determine whether a rule’s actions should execute. Conditions can be combined using logical grouping:

- **`all`**: All nested conditions must be true.
- **`any`**: At least one nested condition must be true.

You can nest `all` and `any` blocks arbitrarily for complex logic. Each `all` or `any` block contains a list of `condition` entries.

### Condition Types

1.  **Comparison Condition**:

        condition:
          type: comparison
          data_source: <data_key>
          operator: "<|>|<=|>=|==|!="
          value: <number>

    - Compares the current data source value to a given literal number.
    - Example:

            condition:
              type: comparison
              data_source: humidity
              operator: "<"
              value: 30

2.  **Threshold Over Time Condition**:

        condition:
          type: threshold_over_time
          data_source: <data_key>
          threshold: <number>
          duration: <time_span>

    - Checks if a data source’s value has stayed above (or below, based on comparison) a given threshold for a specified duration.
    - The comparison is always `>` (greater than) the threshold. If a `<` threshold is needed, consider inverting logic or using comparison conditions combined with a temporal mechanism.
    - Duration is specified as a string, e.g. `500ms`, `2s`.
    - Example:

            condition:
              type: threshold_over_time
              data_source: temperature
              threshold: 50
              duration: 500ms

3.  **Expression Condition**:

        condition:
          type: expression
          expression: "(<data_key> ± <number> ... ) <operator> <number>"

    - Arbitrary arithmetic on one or more data sources is allowed, followed by a comparison.
    - Supported arithmetic: `+`, `-`, `*`, `/`
    - Supported comparison operators: `<`, `>`, `<=`, `>=`, `==`, `!=`
    - Parentheses for grouping are allowed.
    - Example:

            condition:
              type: expression
              expression: "(temperature - 32) * (5/9) > 10"

### Logical Grouping Examples

**Using `all`:**

    conditions:
      all:
        - condition: { ... }  # Must be true
        - condition: { ... }  # Must be true

**Using `any`:**

    conditions:
      any:
        - condition: { ... }  # If any one of these is true
        - condition: { ... }

**Nested conditions:**

    conditions:
      all:
        - condition:
            type: comparison
            data_source: humidity
            operator: "<"
            value: 30
        - any:
            - condition:
                type: comparison
                data_source: temperature
                operator: ">"
                value: 50
            - condition:
                type: expression
                expression: "(temperature - 32) * (5/9) > 10"

## Actions

Actions are triggered if the conditions of a rule are met. Actions are executed asynchronously. Certain actions may be batched before updating the KV store for efficiency.

### Supported Actions

1.  **Set Value Action**:

        - set_value:
            key: <string>
            value: <string>

    OR using an expression:

        - set_value:
            key: <string>
            value_expression: <expression_string>

    - This writes a key/value pair back into the KV store (Redis).
    - `value` is a literal string.
    - `value_expression` allows arithmetic on data source values before setting the key.

2.  **Send Message Action**:

        - send_message:
            channel: <string>
            message: <string>

    - Sends a message to an external system (e.g., a messaging queue) asynchronously.

## Validation

- **Schema Validation**:
  A JSON Schema (external document) will validate:

      - Proper structure of `rules.yaml` (presence of `version`, `valid_data_sources`, and `rules`).
      - Each rule’s fields and condition/action structures.
      - Data source references must appear in `valid_data_sources`.
      - Operators and condition types must be known and valid.

- **Compilation Validation**:
  The compiler will:

      - Check for unknown data sources not listed in `valid_data_sources`.
      - Validate arithmetic expressions for syntax errors.
      - Ensure no circular dependencies between rules.
      - Produce clear, descriptive errors if invalid references or structures are found.

## Compilation & Generated Code

- **Process**:

  - The DSL is parsed into an internal representation.
  - The compiler identifies dependencies (e.g., which data sources each rule needs, which keys get set).
  - Dependencies and ordering are optimized to minimize redundant evaluations.
  - Temporal conditions trigger the generation of code that references runtime-provided temporal state management.
  - The compiler emits C# code split into multiple files for manageability (e.g., 100 rules per file).
  - All code files may be combined into a single assembly/class via partial classes.

- **Runtime Integration**:

  - The runtime passes data source values into the compiled methods every cycle (100ms).
  - The compiled code evaluates rules and triggers actions as needed.
  - The runtime manages temporal states and caching for data sources that require it.

## Metadata Generation

- **Rule Metadata Class**:
  The compiler generates a `RuleMetadata` class that provides a dictionary of rule names to `RuleInfo` objects. Each `RuleInfo` includes:

  - Rule name
  - Description
  - Referenced data sources
  - Actions performed
  - Condition types

- **Uses of Metadata**:

  - Runtime debugging: Inspect which rules are defined and what they depend on.
  - Dependency visualization: Understand how rules interact without reading YAML or C# code.
  - Documentation generation: Transform metadata into human-friendly docs.
  - Performance optimization: Identify which rules depend on which data sources to skip unneeded evaluations.

## Error Handling & Logging

- **Compilation Errors**:
  If a rule references an unknown data source or uses an invalid operator, the compiler fails with a descriptive error.
- **Runtime Logging**:

  - Optional hooks for logging rule evaluations, condition checks, and action executions.
  - Metadata can be leveraged to provide clear and meaningful log messages.

## Lifecycle

- **Development Time**:
  The rules are created and validated using the DSL. The code is compiled and tested.
- **Deployment**:
  Once deployed, the rules do not change frequently. If changes occur, the entire set is re-validated and re-compiled.

---

**This DSL Specification** should be used as the primary reference for designing and implementing the parser, compiler, runtime engine, and associated tooling. It defines the structure, capabilities, and requirements for the rules engine DSL.
