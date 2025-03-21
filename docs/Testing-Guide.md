# Testing Guide

## Overview

This guide covers how to test the Pulsar rule compilation system and the Beacon runtime environment. With the transition to a template-based code generation approach now complete, the testing framework has been updated to validate the template-generated code. The testing framework validates the following components:
1. Rule parsing and validation
2. Rule compilation into C# code using templates from Pulsar.Compiler/Config/Templates
3. AOT compilation of the generated code
4. Runtime execution in the Beacon environment
5. Performance and memory usage 
6. Temporal rule behavior with object-value buffer caching

## Test Categories

### Rule Parsing Tests

These tests validate that YAML rules can be correctly parsed into `RuleDefinition` objects.

### AOT Compilation Tests

These tests verify that the generated C# code can be compiled with AOT (Ahead-of-Time) compilation, which is essential for running in environments where Just-In-Time (JIT) compilation is not available. The templates in Pulsar.Compiler/Config/Templates are designed to be fully AOT-compatible.

Key aspects tested:
- No use of reflection in the generated code or templates
- Full compatibility with trimming
- Support for PublishTrimmed and PublishReadyToRun
- Proper use of JSON serialization contexts
- Elimination of dynamic code generation

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

These tests verify the circular buffer implementation that allows rules to reference historical values. The buffer now supports generic object values instead of just numeric values, enabling more complex temporal rule scenarios.

Key aspects tested:
- Storage and retrieval of various data types (numeric, string, complex objects)
- Time-based filtering with different durations
- Thread-safety under concurrent access
- Memory efficiency with large datasets
- Proper handling of time windows and thresholds

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

## Test Implementation

### Redis Integration Tests using TestContainers

The Redis integration tests use TestContainers to ensure that the Redis service works correctly in various deployment configurations.

Key components:
- `RedisTestFixture` class that manages a Redis container for tests
- Tests for basic Redis operations (Get, Set)
- Tests for sending and receiving messages
- Tests for object serialization and deserialization
- Tests for Redis connection and retry logic
- Tests for different Redis deployments
- Tests for error handling and retry mechanism

### Performance Benchmarks for Large Rule Sets

The performance benchmarks measure the rule evaluation performance with different sizes of rule sets.

Key components:
- `Pulsar.Benchmarks` project with BenchmarkDotNet configuration
- `RuleSetGenerator` class to generate rules of different complexity
- Benchmarks for evaluating rules with different counts and complexities
- Memory diagnostics to monitor memory usage during benchmarks
- Parameterized benchmarks for different rule types and sizes

### AOT Compatibility Tests Across Platforms

The AOT compatibility tests ensure that the code works correctly on different platforms when compiled with AOT.

Key components:
- Test matrix for Windows x64 and Linux x64 with net9.0
- `PlatformCompatibilityTests` class for cross-platform testing
- Tests to verify AOT-specific attributes in generated code
- Validation for trimming configuration in project files
- Tests for proper JSON serialization context
- Tests for the circular buffer with object values

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

## Success Criteria

The testing suite is considered complete when:

1. All Redis integration tests pass on all supported deployment configurations
2. Performance benchmarks are established and documented
3. AOT compatibility tests pass on supported platforms

## Next Steps

1. **Continue Regular Test Runs**
   - Run the complete test suite regularly to catch regressions
   - Update tests as needed when new features are added
   - Expand the test matrix to include more platforms as needed

2. **Integrate with CI/CD**
   - Set up automated test runs in the CI/CD pipeline
   - Configure platform-specific test matrix in CI/CD
   - Add performance benchmark tracking to detect performance regressions

3. **Expand Test Coverage**
   - Add more edge cases to Redis integration tests
   - Create additional complexity levels for benchmark tests
   - Expand platform support for AOT compatibility tests
