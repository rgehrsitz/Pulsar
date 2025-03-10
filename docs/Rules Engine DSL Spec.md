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

## Data Sources
- **Redis as the Primary Data Source**: All sensor data is fetched from Redis during each evaluation cycle.
- **Temporal Data Caching**: For rules that require historical values, the system **caches** previous values in an in-memory ring buffer to support temporal evaluations.
- **External Actions**: Rules may trigger actions such as REST API calls but cannot pull external data sources into the rule evaluation process.

## Rule Format
### Current Implementation
Rules are defined in YAML using this structure:
```yaml
rules:
  - name: "ExampleRule"      # Required, unique identifier
    description: "..."       # Optional description
    conditions:             # Required condition group
      all:                  # or 'any'
        - condition:        # Individual condition
            type: "comparison|threshold_over_time|expression"
            # Additional fields based on type
    actions:               # Required list of actions
      - action_type:       # set_value or send_message
          # Action-specific fields
```

### Temporal Handling
- **Default Mode: Strict Discrete**: The system only trusts explicit data points. If no new reading is available within a time window, it assumes no change has occurred.
- **Extended Last-Known Mode**: If enabled in the DSL, missing data points will be interpolated using the last known value until a new data point contradicts it.
- **Temporal rules utilize an in-memory ring buffer** to retain historical values needed for evaluation.

### Supported Condition Types
1. **Comparison Condition**
```yaml
condition:
  type: comparison
  sensor: "sensor_name"
  operator: "<|>|<=|>=|==|!="
  value: <number>
```

2. **Expression Condition**
```yaml
condition:
  type: expression
  expression: "sensor_a + (sensor_b * 2) > 100"
```

3. **Threshold Over Time**
```yaml
condition:
  type: threshold_over_time
  sensor: "sensor_name"
  threshold: <number>
  duration: <milliseconds>
```

### Rule Dependencies
- **Rules can reference each other’s outputs.**
- The system sorts rules into **logical evaluation layers** so that dependencies are resolved before dependent rules execute.
- **Circular dependencies are detected at build time** and will result in a compilation error.

### Supported Actions
1. **Set Value**
```yaml
set_value:
  key: "output_sensor"
  value_expression: "sensor_a * 2"
```

2. **Send Message**
```yaml
send_message:
  channel: "alert_channel"
  message: "Alert text"
```

### Execution Order
- **Actions within a rule execute in sequence**, in the order they appear in the YAML definition.
- **Rules execute in dependency-resolved layers**, ensuring that dependent rules always have the latest computed values.

## Compilation Process
1. **Validation Phase**
   - Validate YAML structure, sensor references, and expressions.
   - Detect circular dependencies.

2. **Dependency Analysis**
   - Compute evaluation order and assign rule layers.

3. **Code Generation**
   - Generate optimized C# code for execution.
   - Include temporal buffer logic if needed.

## Expected Output
Once compiled, the system will produce:
- A complete, **self-contained C# project** that integrates with Redis.
- Precompiled rule logic with optimized execution.
- A fully **AOT-compatible** binary for deployment.

---

This updated document now fully aligns with the clarified expectations, including **temporal caching**, **evaluation ordering**, **circular dependency handling**, and **action execution sequencing**.

