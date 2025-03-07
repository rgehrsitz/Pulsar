# Rules Engine

## Overview

Pulsar is a high-performance, polling-based rules evaluation engine designed to process hundreds to thousands of key/value inputs using Redis as its primary data store. It fetches inputs, applies rules, and writes outputs back on a configurable schedule (default 100ms). The system's primary goal is to provide deterministic, real-time evaluations with minimal runtime overhead.

## Key Concepts

### Rule Definitions (YAML/DSL)

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

## Rule Dependencies and Execution Order

- Rules can reference **computed values from prior layers**.
- The system performs a **topological sort** to determine rule execution order.
- **Circular dependencies cause build-time failure**.
- **Maximum allowed dependency depth** is configurable (default: 10 levels) with warnings for deep chains.
- Actions execute **sequentially in defined order**.

## Temporal Handling

- **Default Mode: Strict Discrete**: The system only trusts explicit data points. If no new reading is available within a time window, it assumes no change has occurred.
- **Extended Last-Known Mode**: If enabled in the DSL, missing data points will be interpolated using the last known value until a new data point contradicts it.
- **Temporal rules utilize an in-memory ring buffer** to retain historical values needed for evaluation.

## Build-Time Compilation

The compiler **validates rule syntax**, **performs dependency analysis**, and **generates optimized C# code**. The compiled output is a **complete, standalone C# solution** named **Beacon**, containing:
- `Beacon.sln` - The main solution file
- `Beacon.Runtime/` - The main runtime project
  - `Beacon.Runtime.csproj` - Runtime project file
  - `Generated/` - Contains all generated rule files
  - `Services/` - Core runtime services
- `Beacon.Tests/` - Test project for generated rules
  - `Beacon.Tests.csproj` - Test project file
  - `Generated/` - Generated test files

## Runtime Execution

The **Beacon.Runtime** executable fetches **bulk sensor values from Redis every 100ms**. **Rules execute in dependency-resolved layers** to maintain deterministic evaluation. Computed results are **written back to Redis** after processing.

The execution sequence is as follows:

```
sequenceDiagram
    participant Timer
    participant Runtime
    participant Redis
    participant RingBuffer
    participant Rules
    
    Note over Timer,Rules: 100ms Cycle
    Timer->>Runtime: Trigger Cycle
    activate Runtime
    
    Runtime->>Redis: Bulk Fetch (Pipeline)
    Redis-->>Runtime: Sensor Values
    
    Runtime->>RingBuffer: Update Historical Values
    Runtime->>Rules: Layer 0 Evaluation
    Runtime->>Rules: Layer 1 Evaluation
    Runtime->>Rules: Layer N Evaluation
    
    Runtime->>Redis: Bulk Write Results (Pipeline)
    Redis-->>Runtime: Confirmation
    
    Runtime->>Runtime: Calculate Cycle Stats
    Runtime-->>Timer: Complete Cycle
    deactivate Runtime
    
    Note over Timer,Rules: Next 100ms Cycle
```

## Compilation Process

1. **Validation Phase**
   - Validate YAML structure, sensor references, and expressions.
   - Detect circular dependencies.

2. **Dependency Analysis**
   - Compute evaluation order and assign rule layers.

3. **Code Generation**
   - Generate optimized C# code for execution.
   - Include temporal buffer logic if needed.

## Performance and Stability

- **Deterministic timing**: Ensures all evaluations complete within the configured cycle time.
- **Minimal overhead**: Uses **precompiled code** and **index-based lookups** instead of dictionaries.
- **Scalability**: Capable of handling thousands of rules efficiently.

## Example Rule

```yaml
rules:
  - name: "TemperatureAlert"
    description: "Sends an alert when temperature is above threshold for 5 minutes"
    conditions:
      all:
        - condition:
            type: threshold_over_time
            sensor: "temperature"
            operator: ">"
            threshold: 80
            duration: 300000  # 5 minutes in milliseconds
    actions:
      - send_message:
          channel: "alerts"
          message: "Temperature has been above 80 degrees for 5 minutes"
      - set_value:
          key: "alert_status"
          value: "active"
```

## Best Practices

1. **Rule Organization**
   - Group related rules together
   - Use clear, descriptive names
   - Add meaningful descriptions

2. **Dependency Management**
   - Keep dependency chains short
   - Avoid complex circular references
   - Use layering to organize rule execution

3. **Performance Optimization**
   - Minimize the number of conditions per rule
   - Use simple expressions where possible
   - Only use temporal conditions when necessary

4. **Testing**
   - Create test cases for each rule
   - Verify rule behavior with different inputs
   - Test edge cases and boundary conditions
