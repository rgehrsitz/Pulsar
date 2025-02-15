# Pulsar System Refactoring Plan

## Overview
Pulsar is evolving into a fully **AOT-compatible** rules evaluation system. The previous **Pulsar.Runtime** project has been deprecated, and its relevant classes and methods have been **migrated into Pulsar.Compiler**. This refactoring effort ensures that Pulsar functions as a **code generation tool** that outputs a complete, **standalone C# project** capable of executing all required runtime functionality, including Redis data integration, rule evaluation, and error handling.

## Goals
- **AOT Compatibility**: Ensure the generated project supports **AOT compilation** and produces a fully standalone executable.
- **Complete Standalone Execution**: The generated project should serve as a **self-sufficient runtime environment**.
- **Enhanced Debugging**: Improve traceability between source rules and generated code for maintainability and debugging.
- **Build-Time Rule Processing**: Move all rule compilation and processing to **build time**.
- **Scalability**: Support **hundreds to thousands** of rules with maintainable code organization.
- **Maintainability**: Improve code clarity and **eliminate dynamic constructs** such as reflection and runtime code generation.

## Key Changes
### 1. Deprecate Pulsar.Runtime
- All runtime logic is migrated into **Pulsar.Compiler**.
- The final output must be a **fully functional, self-contained C# project**.

### 2. Generate a Complete Standalone Project
- Pulsar will output a directory containing:
  - `.sln` (solution) file.
  - `.csproj` (project) files.
  - `.cs` source files.
  - **Pre-written templates** for core runtime logic.
  - **Generated rule logic** in optimized C#.
- The solution must be **AOT-compatible** and able to be built using:
  ```sh
  dotnet publish -c Release -r <runtime> --self-contained true
  ```
- This standalone executable **will be deployed without any external dependencies**.

### 3. Redis as the Primary Data Source with Temporal Data Caching
- The generated solution will use **Redis** as the exclusive mechanism for fetching and storing system data.
- **Exception:** For **temporal rules**, the runtime engine must **cache the previous X number of values** in memory to support temporal evaluations.
- Actions within rules may **send messages to external REST endpoints** but cannot use other data storage mechanisms.

### 4. Eliminate Runtime Code Generation
- **All rules are precompiled** into C# source files.
- No runtime `Reflection.Emit`, dynamic assemblies, or code injection.
- **Remove all dynamic loading mechanisms** from the system.

### 5. Maintain Rule Traceability
- Generated files must include **comments and metadata** tracing rules back to their original DSL/YAML definitions.
- If a rule was split into multiple files, the manifest must track these changes.

### 6. Improve Build-Time Processing
- A dedicated **build-time orchestrator** will:
  - Read **YAML rule files**.
  - Validate syntax and semantics.
  - Detect and **prevent circular dependencies**.
  - Manage **file splitting/grouping**.
  - Generate **all necessary C# source files**.
- The orchestrator must be configurable via **MSBuild integration**.

## Implementation Phases
### Phase 1: Code Generation Updates
- Update `Pulsar.Compiler` to:
  - Generate **C# source files** instead of emitting runtime DLLs.
  - Include **rule metadata** in comments for traceability.
  - **Support file splitting** for large rule sets.
  - **Eliminate reflection/dynamic code.**
  - Generate a **manifest** tracking all output files.

### Phase 2: Build Process Integration
- Implement a **build-time orchestrator** that:
  - Reads **YAML rule files**.
  - Validates and compiles them into **C# code**.
  - Manages **file splitting/grouping**.
  - Outputs a **fully functional project directory**.
- Integrate with **MSBuild**:
  - Define a **pre-build step** to run the orchestrator.
  - Allow configuration of **file thresholds and output paths**.

### Phase 3: Runtime System Execution
- The **generated standalone executable** will:
  - **Fetch data from Redis every 100ms**.
  - **Process rules in evaluation layers**, ensuring dependencies are satisfied.
  - **Write outputs back to Redis**.
  - Perform **logging, error handling, and observability**.
  - **Cache previous values for temporal rules** to support time-based evaluations.
- No external runtime dependencies will be required.

### Phase 4: AOT Compatibility
- Audit and **remove**:
  - **Dynamic code generation**.
  - **Runtime reflection**.
  - **Dynamic loading of assemblies**.
- Add **AOT compatibility attributes** where needed.
- Ensure all dependencies support **AOT compilation**.
- Conduct **AOT compilation testing**.

### Phase 5: Testing & Documentation
- Implement **integration tests** covering:
  - **Large rule sets**.
  - **File splitting mechanisms**.
  - **Full build process validation**.
- Update documentation for:
  - **Build process setup**.
  - **Rule organization**.
  - **Debugging compiled code**.

## Expected Output
Once the changes are implemented, Pulsar will function as a **purely static code generator**, ensuring that:
- The **output directory** contains a complete **standalone C# project**.
- No runtime-generated code exists.
- The solution **can be built separately** via MSBuild or command-line tools.
- The final binary is fully **AOT-compatible**.
- The generated solution includes all **required runtime execution functionality**.

## Summary of Clarifications
| Topic | Clarification |
|-------|--------------|
| **Runtime Execution** | The **generated project** must implement **all runtime logic**, including the 100ms execution loop. |
| **Redis Requirement** | Redis is the **primary** data source, with an exception for **temporal caching** in memory. |
| **Temporal Mode** | **Strict Discrete Mode** is the default. **Extended Last-Known Mode** must be explicitly defined in the DSL. |
| **Rule Dependencies** | Rules **can reference each other’s outputs**. Circular dependencies **must be detected** at build time. |
| **Action Execution Order** | Actions execute **in sequence**, in the order they are written in the rule definition. |
| **CI/CD Integration** | The user is responsible for compiling the generated project. Future CI/CD automation is possible. |

---

This document now fully aligns with the clarified requirements, including the explicit handling of temporal caching. Let me know if further refinements are needed.

