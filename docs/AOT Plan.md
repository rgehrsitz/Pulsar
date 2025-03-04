# Pulsar System Refactoring Plan

## Overview
Pulsar is evolving into a fully **AOT-compatible** rules evaluation system. The previous **Pulsar.Runtime** project has been deprecated, and its relevant classes and methods have been **migrated into Pulsar.Compiler**. This refactoring effort ensures that Pulsar functions as a **code generation tool** that outputs a complete, **standalone C# project** named **Beacon** that contains all required runtime functionality, including Redis data integration, rule evaluation, and error handling.

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

### 2. Generate a Complete Standalone Project (Beacon)
- Pulsar will output a directory containing the **Beacon** solution with the following structure:
  - `Beacon.sln` - The main solution file
  - `Beacon.Runtime/` - The main runtime project
    - `Beacon.Runtime.csproj` - Runtime project file
    - `Generated/` - Contains all generated rule files
    - `Services/` - Core runtime services
    - `Models/` - Data models and configurations
    - `Interfaces/` - Service and component interfaces
    - `Buffers/` - Temporal data caching components
    - `Rules/` - Base rule implementations
  - `Beacon.Tests/` - Test project for generated rules
    - `Beacon.Tests.csproj` - Test project file
    - `Generated/` - Generated test files
- The solution must be **AOT-compatible** and able to be built using:
  ```sh
  dotnet publish -c Release -r <runtime> --self-contained true
  ```
- This standalone executable **will be deployed without any external dependencies**.

### 3. Redis as the Primary Data Source with Temporal Data Caching
- The generated solution will use **Redis** as the exclusive mechanism for fetching and storing system data.
- **Exception:** For **temporal rules**, the runtime engine must **cache the previous X number of values** in memory to support temporal rule evaluation.
- The Redis integration includes:
  - **Connection Pooling**: Efficient management of Redis connections
  - **Health Monitoring**: Continuous monitoring of Redis connection health
  - **Metrics Collection**: Tracking of Redis operations and errors
  - **Error Handling**: Robust error handling and retry mechanisms
  - **Configuration**: Flexible configuration options for Redis connections

### 4. Rule Group Organization
- Rules are organized into **rule groups** for better maintainability and performance.
- Each rule group is generated as a **separate class** with its own evaluation method.
- Rule groups are evaluated in a **coordinated manner** to ensure proper execution order.

## Implementation Progress

### Completed
- Basic project structure and code generation
- Rule group organization and code generation
- Temporal data caching implementation
- Redis service integration with connection pooling
- Health monitoring and metrics collection
- Error handling and retry mechanisms
- Logging using Microsoft.Extensions.Logging

### In Progress
- Comprehensive testing of generated code
- Performance optimization
- Documentation updates

### Pending
- CI/CD integration
- Deployment automation
- Advanced monitoring and alerting

## Phases of Implementation

### Phase 1: Code Generation (Completed)
- Implement the basic code generation framework.
- Generate rule groups and evaluation methods.

### Phase 2: Redis Integration (Completed)
- Implement Redis service with connection pooling.
- Add health monitoring and metrics collection.
- Implement error handling and retry mechanisms.

### Phase 3: Testing and Optimization (In Progress)
- Test the generated code with various rule sets.
- Optimize performance for large rule sets.
- Update documentation.

### Phase 4: AOT Compatibility (Completed)
- Audit and **remove**:
  - **Dynamic code generation**.
  - **Runtime reflection**.
  - **Dynamic loading of assemblies**.
- Ensure all code is **AOT-compatible**.

### Phase 5: Deployment and Automation (Pending)
- Implement CI/CD integration.
- Automate deployment process.
- Add advanced monitoring and alerting.

## Conclusion
This refactoring effort will result in a more maintainable, scalable, and performant rules evaluation system that is fully AOT-compatible and can be deployed as a standalone executable.
