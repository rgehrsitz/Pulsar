# Pulsar System Overview

## What is Pulsar?

Pulsar is a high-performance, polling-based rules evaluation engine designed to process hundreds to thousands of key/value inputs using Redis as its primary data store. It fetches inputs, applies rules, and writes outputs back on a configurable schedule (default 100ms). The system's primary goal is to provide deterministic, real-time evaluations with minimal runtime overhead.

## Key Concepts and Components

### 1. Rule Definitions (YAML/DSL)

- Rules are authored using a human-readable DSL in YAML format.
- Each rule defines **conditions** (comparisons, arithmetic operations) and **actions** (set values, send messages).
- Rules **can reference each other's outputs**, forming **dependency layers**.
- Circular dependencies **must be detected at build time** and will fail compilation.
- **Maximum allowed dependency depth** should be configurable (default: 10 levels) with warnings for deep chains.

### 2. Build-Time Compilation

- The compiler **validates rule syntax**, **performs dependency analysis**, and **generates optimized C# code**.
- The compiled output is a **complete, standalone C# solution** named **Beacon**, containing:
  - `Beacon.sln` - The main solution file
  - `Beacon.Runtime/` - The main runtime project
    - `Beacon.Runtime.csproj` - Runtime project file
    - `Generated/` - Contains all generated rule files
    - `Services/` - Core runtime services
  - `Beacon.Tests/` - Test project for generated rules
    - `Beacon.Tests.csproj` - Test project file
    - `Generated/` - Generated test files
- The system sorts rules into **evaluation layers** to ensure dependencies are resolved before execution.
- **Ring Buffer Configuration**: Temporal buffers are configured at **build-time** to optimize memory and resource usage based on rule definitions.
- **Pre-written templates** stored within the Pulsar repository ensure versioning and consistency.

### 3. Runtime Execution

- The **Beacon.Runtime** executable fetches **bulk sensor values from Redis every 100ms**.
- **Rules execute in dependency-resolved layers** to maintain deterministic evaluation.
- Computed results are **written back to Redis** after processing.
- **Temporal Data Handling**: The system maintains **an in-memory ring buffer** for historical values needed in temporal rule evaluations.
- **Actions execute sequentially** within a rule, following the order in which they were defined.
- **Structured Logging with Serilog**: The runtime will use **Serilog** for logging, ensuring uniformity with the broader system's logging strategy.
- **Prometheus Metrics Exposure**: The runtime will expose **Prometheus-compatible** metrics for monitoring.
- **Error Handling and Recovery**: Non-critical errors are logged and rule evaluation continues, while critical errors trigger alerts and halt execution when necessary.

### 4. Performance and Stability

- **Deterministic timing**: Ensures all evaluations complete within the configured cycle time.
- **Minimal overhead**: Uses **precompiled code** and **index-based lookups** instead of dictionaries.
- **Scalability**: Capable of handling thousands of rules efficiently.

## Rules Engine Requirements

### **Rules (DSL) Requirements**

1. **Language & Syntax**

   - Supports **comparison, arithmetic, and temporal expressions**.
   - **Strict Discrete Mode is the default** (values are not assumed unless explicitly reported).
   - **Extended Last-Known Mode** must be explicitly enabled where needed.

2. **Dependency Management**

   - Rules can reference **computed values from prior layers**.
   - A **topological sort** determines rule execution order.
   - **Circular dependencies cause build-time failure**.

3. **Action Execution**

   - Actions execute **in sequence**, in the order they are defined.
   - Supported actions: **Set Value, Send Message**.

### **Compiler Requirements**

1. **Parsing and Validation**

   - Ensures correct syntax and references.
   - Detects **cycles in rule dependencies** and rejects invalid configurations.

2. **Code Generation**

   - Produces a complete **Beacon solution** with runtime and test projects.
   - Includes **rule tracking metadata** for debugging.
   - Generates appropriate **project files** and solution structure.
   - Places generated code in the correct project directories.

3. **Build Integration**

   - Generates an **AOT-compatible Beacon solution**.
   - Users compile using:
     ```sh
     dotnet publish -c Release -r <runtime> --self-contained true
     ```
   - **Optional CI/CD script generation** for automating builds.
   - **Automated versioning** embedded in generated project metadata.
   - **Inclusion of test files** in the Beacon.Tests project to validate rule execution correctness.

### **Runtime Requirements**

1. **Redis Integration**

   - The **Beacon.Runtime** project:
     - **Bulk fetches all input values every 100ms**.
     - **Writes computed outputs back to Redis** after evaluation.
     - **Does not store intermediate values in Redis** between rule evaluations.
     - **Supports both single-instance and clustered Redis configurations**.
     - **Retry mechanisms with exponential backoff** in case of connectivity issues.

2. **Temporal Handling**

   - **Ring buffer stores previous values** for temporal rule evaluation.
   - Only **sensor values explicitly needing history are cached**.

3. **Execution Order**

   - **Rules execute layer-by-layer**, ensuring dependencies are satisfied.
   - **Actions within a rule execute sequentially**.

4. **Logging & Monitoring**

   - **Serilog for Structured Logging**: All logs will be structured and follow system-wide conventions.
   - **Prometheus Metrics Exposure**: The runtime will expose **performance metrics** in a Prometheus-compatible format.
   - **Error handling mechanisms** include structured error logging, recovery strategies, and alerts for critical failures.

### **CI/CD Integration**

- **Manual Compilation for Now**: The generated Beacon solution is manually compiled by users.
- **Future CI/CD automation** may include:
  - **Build script generation** for common CI/CD platforms.
  - **Automated versioning** embedded in generated project metadata.
  - **Test execution** using the Beacon.Tests project to validate rule execution correctness.

---

## Summary of Clarifications

| Topic                      | Clarification                                                                                 |
| -------------------------- | --------------------------------------------------------------------------------------------- |
| **Project Structure**      | Generated code is organized in a **Beacon solution** with runtime and test projects.          |
| **Standalone Execution**   | The **Beacon.Runtime** project serves as a **self-contained runtime**.                        |
| **Redis Requirement**      | Redis is the **primary data source**, but **temporal rules rely on in-memory caching**.       |
| **Temporal Mode**          | **Strict Discrete Mode** is default; **Extended Last-Known Mode** must be explicitly enabled. |
| **Rule Dependencies**      | Rules **can reference other rule outputs**; circular dependencies **cause build failure**.    |
| **Maximum Dependency Depth** | Configurable (default: 10 levels) with warnings for deep chains.                            |
| **Action Execution Order** | Actions execute **sequentially in defined order**.                                            |
| **CI/CD Integration**      | Users currently **manually compile** the Beacon solution; CI/CD automation may follow.        |
| **Logging Framework**      | The runtime will use **Serilog** for structured logging.                                       |
| **Monitoring**             | The runtime will expose **Prometheus-compatible metrics** for monitoring.                      |
| **Error Handling**         | Logs errors using **Serilog**, applies retry strategies, and triggers alerts when necessary.  |

---

This document now fully aligns with the clarified system behavior, execution model, structured logging with Serilog, Prometheus monitoring, Redis failover handling, and build-time configurations. Let me know if further refinements are needed!
