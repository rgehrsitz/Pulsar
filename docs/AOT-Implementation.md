# AOT Implementation

## Overview
Pulsar is evolving into a fully **AOT-compatible** rules evaluation system. The previous **Pulsar.Runtime** project has been deprecated, and its relevant classes and methods have been **migrated into Pulsar.Compiler**. This refactoring effort ensures that Pulsar functions as a **code generation tool** that outputs a complete, **standalone C# project** named **Beacon** that contains all required runtime functionality, including Redis data integration, rule evaluation, and error handling.

## Goals
- **AOT Compatibility**: Ensure the generated project supports **AOT compilation** and produces a fully standalone executable.
- **Complete Standalone Execution**: The generated project should serve as a **self-sufficient runtime environment**.
- **Enhanced Debugging**: Improve traceability between source rules and generated code for maintainability and debugging.
- **Build-Time Rule Processing**: Move all rule compilation and processing to **build time**.
- **Scalability**: Support **hundreds to thousands** of rules with maintainable code organization.
- **Maintainability**: Improve code clarity and **eliminate dynamic constructs** such as reflection and runtime code generation.

## Key Components Implemented

1. **CodeGenerator with Fixed Generators**: Updated implementation that uses RuleGroupGeneratorFixed to ensure proper AOT compatibility and SendMessage support.

2. **BeaconTemplateManager**: A template manager that creates a complete solution structure with:
   - Beacon.sln solution file
   - Beacon.Runtime project
   - Beacon.Tests project
   - Full directory structure and dependencies

3. **BeaconBuildOrchestrator**: An orchestrator that:
   - Takes parsed rules and system configuration
   - Generates a complete solution
   - Compiles the rules into C# source files
   - Builds the solution using dotnet CLI

4. **Enhanced Redis Integration**: Complete implementation of Redis services with:
   - Connection pooling
   - Health monitoring
   - Metrics collection
   - Error handling and retry mechanisms
   - Support for various deployment configurations

5. **Temporal Rule Support**: Improved implementation of circular buffer for temporal rules with:
   - Support for object values instead of just doubles
   - Efficient memory usage
   - Time-based filtering capabilities
   - Thread-safe operations

## Implementation Status

### Completed
- Basic project structure and code generation
- Rule group organization and code generation
- Temporal data caching implementation
- Redis service integration with connection pooling
- Health monitoring and metrics collection
- Error handling and retry mechanisms
- Logging using Microsoft.Extensions.Logging
- Namespace and serialization fixes
- SendMessage method implementation
- Object value support in CircularBuffer
- JSON serialization for AOT compatibility

### In Progress
- Comprehensive testing of generated code
- Performance optimization
- Documentation updates

### Pending
- CI/CD integration
- Deployment automation
- Advanced monitoring and alerting

## Recent Fixes

1. **Namespace and Serialization Fixes**
   - Removed references to non-existent Generated namespace in Program.cs template
   - Removed Generated namespace import from BeaconTemplateManager.cs
   - Added JSON Serialization Context for AOT serialization in Program.cs template
   - Added proper JsonSerializable attributes for commonly serialized types

2. **Rule Generation Fixes**
   - Created RuleGroupGeneratorFixed class that includes SendMessage method
   - Created proper namespace for EmbeddedConfig
   - Added SystemConfigJson constant for AOT compatibility
   - Created CodeGeneratorFixed class that uses the improved rule generators
   - Created BeaconBuildOrchestratorFixed with improved code generators and template managers

3. **Object Value Support**
   - Modified RingBufferManager to handle generic object values instead of just doubles
   - Added new GetValues(string, TimeSpan) method for temporal filtering with duration
   - Updated buffer storage from `Queue<(DateTime, double)>` to `Queue<(DateTime, object)>`
   - Changed TimestampedValue struct to use object instead of double
   - Added proper Convert.ToDouble calls in threshold comparisons

## Project Structure

```
Beacon/
├── Beacon.sln                  # Main solution file
├── Beacon.Runtime/             # Main runtime project
│   ├── Beacon.Runtime.csproj   # Runtime project file
│   ├── Program.cs              # Main entry point with AOT attributes
│   ├── RuntimeOrchestrator.cs  # Main orchestrator
│   ├── RuntimeConfig.cs        # Configuration handling
│   ├── Generated/              # Generated rule files
│   │   ├── RuleGroup0.cs       # Generated rule implementations
│   │   ├── RuleCoordinator.cs  # Coordinates rule execution
│   │   └── rules.manifest.json # Manifest of all rules
│   ├── Services/               # Core runtime services
│   │   ├── RedisConfiguration.cs
│   │   ├── RedisService.cs
│   │   └── RedisMonitoring.cs
│   ├── Buffers/                # Temporal rule support
│   │   └── CircularBuffer.cs   # Implements circular buffer for temporal rules
│   └── Interfaces/             # Core interfaces
│       ├── ICompiledRules.cs
│       ├── IRuleCoordinator.cs
│       └── IRuleGroup.cs
└── Beacon.Tests/               # Test project
    ├── Beacon.Tests.csproj     # Test project file
    ├── Generated/              # Generated test files
    └── Fixtures/               # Test fixtures
        └── RuntimeTestFixture.cs
```

## How to Use

### Generating a Beacon Solution

```bash
dotnet run --project Pulsar.Compiler -- beacon --rules=rules.yaml --config=system_config.yaml --output=TestOutput/aot-beacon
```

### Building the Solution

```bash
cd <output-dir>/Beacon
dotnet build
```

### Creating a Standalone Executable

```bash
cd <output-dir>/Beacon
dotnet publish -c Release -r <runtime> --self-contained true
```

## Testing the Implementation

To test these changes:
```bash
# Build the project
dotnet build

# Run the compiler with beacon template generation
dotnet run --project Pulsar.Compiler -- beacon --rules=rules.yaml --config=system_config.yaml --output=TestOutput/aot-beacon

# Verify the output files include the SendMessage method and SerializationContext
ls -l TestOutput/aot-beacon/Beacon/Beacon.Runtime/Generated/
```

## Future Improvements

1. **MSBuild Integration**: Add MSBuild integration for easier integration into CI/CD
2. **Enhanced Metrics**: Add more detailed metrics for rule evaluation
3. **Observability**: Improve logging and monitoring
4. **Rule Versioning**: Support for rule versioning and hot upgrades
