# Pulsar Testing Guide

This guide covers how to test the Pulsar rule compilation system and the Beacon runtime environment.

## Overview

The Pulsar testing framework validates the following components:
1. Rule parsing and validation
2. Rule compilation into C# code
3. AOT compilation of the generated code
4. Runtime execution in the Beacon environment
5. Performance and memory usage 
6. Temporal rule behavior with buffer caching

## Running Tests

### Basic Tests

Run the entire test suite:
```bash
dotnet test
```

Run specific categories of tests:
```bash
# Run only integration tests
dotnet test --filter "Category=Integration"

# Run only runtime validation tests
dotnet test --filter "Category=RuntimeValidation"

# Run only memory usage tests
dotnet test --filter "Category=MemoryUsage"

# Run only temporal rule tests
dotnet test --filter "Category=TemporalRules"

# Run only AOT compatibility tests
dotnet test --filter "Category=AOTCompatibility"
```

Run a specific test by name:
```bash
dotnet test --filter "FullyQualifiedName=Pulsar.Tests.RuntimeValidation.RealRuleExecutionTests.SimpleRule_ValidInput_ParsesCorrectly"
```

### Testing with Redis

The runtime execution tests require a Redis instance. The tests will automatically start a Redis container using TestContainers, but you need to have Docker installed and running.

If you want to use an existing Redis instance, you can modify the Redis connection string in the `RuntimeValidationFixture.cs` file:

```csharp
_redisConnection = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
```

### Testing with Different Rule Sets

The test suite includes various rule sets for different testing scenarios:

1. **Simple Rules**: Basic arithmetic operations and comparisons
2. **Complex Rules**: Nested conditions and expressions
3. **Temporal Rules**: Rules that track historical values using circular buffers
4. **Performance Test Rules**: Large rule sets for performance benchmarking

To create custom rule files for testing, use the pattern in `RuntimeValidationFixture.cs` and place them in the test output directory.

## Test Descriptions

### Rule Parsing Tests

These tests validate that YAML rules can be correctly parsed into `RuleDefinition` objects.

### AOT Compilation Tests

These tests verify that the generated C# code can be compiled with AOT (Ahead-of-Time) compilation, which is essential for running in environments where Just-In-Time (JIT) compilation is not available.

Key aspects tested:
- No use of reflection in the generated code
- Compatibility with trimming
- Support for PublishTrimmed and PublishReadyToRun

### Runtime Execution Tests

These tests validate the full pipeline:
1. Parse rule definitions
2. Generate C# code
3. Compile with AOT settings
4. Execute the compiled rules against a Redis instance
5. Verify outputs match expected values

### Performance Tests

Performance tests measure:
- Execution time for different rule counts
- Execution time as rule complexity increases
- Memory usage patterns
- Throughput under concurrent load

### Memory Usage Tests

These tests monitor memory usage during extended rule execution to detect potential memory leaks.

### Temporal Rule Tests

These tests verify the circular buffer implementation that allows rules to reference historical values.

## Debugging AOT Builds

When working with AOT compilation issues:

1. Use the verbose build output:
```bash
dotnet publish -c Release -r linux-x64 --self-contained true -v detailed
```

2. Look for ILLink warnings that indicate AOT compatibility issues:
```
ILLink: warning IL2026: Method 'System.Reflection.MethodBase.GetMethodFromHandle' has 
a generic parameter that is an open generic type, which may result in a MissingMetadataException at runtime
```

3. Add necessary trimmer roots in the trimming.xml file:
```xml
<assembly fullname="YourAssembly">
  <type fullname="FullyQualifiedTypeName" preserve="all" />
</assembly>
```

4. Add DynamicDependency attributes to preserve types that are loaded dynamically.

## Adding New Tests

To add new tests:

1. Add a test class to the appropriate category in the `Pulsar.Tests` project
2. Inherit from the relevant test fixture (`RuntimeValidationFixture` for runtime tests)
3. Generate rule files programmatically or copy them to the test output directory
4. Use the BuildTestProject and ExecuteRules methods to validate behavior

## CI/CD Integration

In CI/CD pipelines, ensure that:

1. Redis is available for the runtime execution tests
2. Docker is available for container-based tests
3. The test output directory is properly cleaned between test runs
4. Different runtime identifiers are tested (e.g., linux-x64, win-x64) for AOT compatibility