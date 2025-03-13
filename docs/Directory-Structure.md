# Pulsar Project Directory Structure

This document explains the purpose of each main directory in the Pulsar project and provides guidelines for what should and shouldn't be in each.

## Primary Directories

### Pulsar.Compiler
The compiler component responsible for processing rule definitions and generating Beacon applications.

**Contains:**
- Rule validation and dependency analysis
- Code generation
- Template management
- Configuration processing

**Guidelines:**
- All rule parsing, validation, and code generation logic belongs here
- Template files should be organized in the Templates directory
- Use the "Fixed" versions of managers and generators for future development

### Pulsar.Runtime
Core runtime components that are used by generated Beacon applications.

**Contains:**
- Interface definitions
- Base rule implementations
- Buffer implementations for temporal rules
- Redis service interfaces

**Guidelines:**
- Keep this lean and focused on runtime essentials
- Avoid duplicating code that exists in the Templates
- Ensure all code is AOT-compatible

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
Example implementations and use cases.

**Contains:**
- Sample rule definitions
- Configuration examples
- Demo implementations

**Guidelines:**
- The `output` directories are temporary and regenerated during test runs
- Keep examples simple and focused on demonstrating specific features
- Include clear documentation on how to run each example

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