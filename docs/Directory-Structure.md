# Pulsar Project Directory Structure

This document explains the purpose of each main directory in the Pulsar project and provides guidelines for what should and shouldn't be in each. The project has transitioned from a runtime library approach to a template-based code generation approach, with templates in Pulsar.Compiler/Config/Templates serving as the source of truth.

## Primary Directories

### Pulsar.Compiler
The primary component responsible for processing rule definitions and generating Beacon applications. This is the core of the system, containing all the templates used for code generation.

**Contains:**
- Rule validation and dependency analysis
- Code generation logic
- Template management (Config/Templates directory)
- Configuration processing
- All interfaces and base implementations

**Guidelines:**
- All rule parsing, validation, and code generation logic belongs here
- Template files in Config/Templates are the source of truth for code generation
- Use the "Fixed" versions of managers and generators for all development
- Maintain AOT compatibility in all templates

### Pulsar.Runtime (Deprecated)
Formerly contained core runtime components. This project is being deprecated as all functionality has been migrated to the template-based approach in Pulsar.Compiler/Config/Templates.

**Previously Contained:**
- Interface definitions (now in Templates/Interfaces)
- Base rule implementations (now in Templates/Runtime/Rules)
- Buffer implementations (now in Templates/Runtime/Buffers)
- Redis service interfaces (now in Templates/Runtime/Services)

**Guidelines:**
- Do not add new code to this project
- Refer to templates in Pulsar.Compiler/Config/Templates instead
- For any remaining code, ensure it is AOT-compatible

### Pulsar.Tests
Test suite for the Pulsar compiler and runtime components.

**Contains:**
- Unit tests for parsing, validation, and compilation
- Integration tests
- Performance and memory tests
- Test utilities and helpers

**Guidelines:**
- Organize tests by category (Parsing, Compilation, etc.)
- Keep test fixtures separate from test implementations
- Use descriptive test names following the pattern: `ClassName_Scenario_ExpectedResult`

### Pulsar.Benchmarks
Performance benchmarks for rule evaluation.

**Contains:**
- Benchmark implementations
- Test data generators

**Guidelines:**
- Focus on measuring real-world scenarios
- Keep benchmarks isolated from production code

### Examples
Example implementations and use cases, recently cleaned up to follow best practices.

**Contains:**
- Sample rule definitions (YAML files)
- Configuration examples (YAML files)
- Shell scripts to demonstrate running the compiler
- README.md files explaining purpose and usage

**Guidelines:**
- Only include source files (YAML, scripts) in version control
- All output directories are excluded from version control via .gitignore
- Generated output should be regenerated during test runs, not committed
- Keep examples simple and focused on demonstrating specific features
- Each subdirectory should have its own README.md explaining its purpose
- Examples should demonstrate best practices for rule definition and configuration

### TestData
Test data used for validation and testing.

**Contains:**
- Sample rule definitions
- Test configurations

**Guidelines:**
- Test data should be representative of real-world scenarios
- Include both valid and invalid test cases

### TestOutput
Destination for test-generated output.

**Contains:**
- Generated code from tests
- Test logs and results

**Guidelines:**
- This directory is regenerated during test runs and can be safely deleted
- Do not store permanent files here

### docs
Project documentation.

**Contains:**
- User guides
- Design documents
- Implementation details
- Examples and tutorials

**Guidelines:**
- Keep documentation up-to-date with code changes
- Use clear, consistent formatting
- Include examples for complex concepts

## Template Directory Structure

The templates in Pulsar.Compiler/Config/Templates are the source of truth for code generation. This directory structure is critical for the proper functioning of the system.

### Templates Directory Organization

**Interfaces/**
- Contains interface definitions for the generated code
- ICompiledRules.cs, IRedisService.cs, IRuleCoordinator.cs, etc.

**Runtime/**
- Contains runtime components that are included in the generated code

**Runtime/Buffers/**
- CircularBuffer.cs - Implementation of the temporal buffer
- IDateTimeProvider.cs - Interface for datetime abstraction
- RingBufferManager.cs - Manager for multiple buffers
- SystemDateTimeProvider.cs - Default datetime provider

**Runtime/Models/**
- RedisConfiguration.cs - Configuration for Redis connections
- RuntimeConfig.cs - Configuration for the runtime environment

**Runtime/Rules/**
- RuleBase.cs - Base class for generated rules

**Runtime/Services/**
- RedisService.cs - Implementation of Redis integration
- RedisHealthCheck.cs - Health monitoring for Redis
- RedisMetrics.cs - Metrics collection for Redis

**Project/**
- Generated.sln - Template for the solution file
- Runtime.csproj - Template for the project file
- trimming.xml - Configuration for AOT trimming

## Generated Directories

### Generated Code
The Pulsar compiler generates complete Beacon applications with the following structure:

**Contains:**
- Beacon.sln - Main solution file
- Beacon.Runtime/ - Main runtime project
  - Program.cs - Entry point with AOT attributes
  - RuntimeOrchestrator.cs - Main orchestrator
  - Generated/ - Generated rule implementations
  - Services/ - Core runtime services
  - Buffers/ - Temporal rule support
  - Interfaces/ - Core interfaces

**Guidelines:**
- Generated code should not be manually modified
- Use the Pulsar compiler to regenerate code when rule definitions change
