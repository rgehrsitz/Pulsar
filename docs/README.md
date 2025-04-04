# Pulsar/Beacon Documentation <img src="pulsar.svg" height="75px">

[![License](https://img.shields.io/badge/License-MIT-blue)](#license)

## Overview

This directory contains the documentation for the Pulsar/Beacon project, a high-performance, AOT-compatible rules evaluation system that uses a template-based code generation approach. The Pulsar compiler processes rule definitions and generates standalone Beacon applications using templates from the `Pulsar.Compiler/Config/Templates` directory. The documentation has been consolidated into focused, comprehensive documents that cover all aspects of the system.

## Documentation Index

### Core Documentation

1. [**Project Status**](Project-Status.md) - Current status of the project, recent fixes, and next steps
2. [**AOT Implementation**](AOT-Implementation.md) - Details of the AOT (Ahead-of-Time) compilation implementation
3. [**Rules Engine**](Rules-Engine.md) - Overview of the rules engine, including rule definitions, conditions, actions, and execution
4. [**Command Line Options**](Command-Line-Options.md) - Comprehensive guide to Pulsar's command line interface and options

### Technical Components

5. [**Redis Integration**](Redis-Integration.md) - Redis integration components, configuration, and best practices
6. [**Prometheus Metrics**](Prometheus-Metrics.md) - Monitoring Beacon with Prometheus metrics
7. [**Temporal Buffer**](Temporal-Buffer.md) - Implementation of the temporal buffer for historical data storage and evaluation

### User Guides

8. [**End-to-End Guide**](End-to-End-Guide.md) - Complete walkthrough from creating YAML rules to running a Beacon application

### Development and Testing

9. [**Testing Guide**](Testing-Guide.md) - Comprehensive guide to testing the Pulsar/Beacon system
10. [**Directory Structure**](Directory-Structure.md) - Explanation of project directories and guidelines

## Getting Started

If you're new to the Pulsar/Beacon project, we recommend starting with the following documents:

1. [**End-to-End Guide**](End-to-End-Guide.md) - Complete walkthrough for new users
2. [**Rules Engine**](Rules-Engine.md) - To learn about rule definitions and capabilities
3. [**Directory Structure**](Directory-Structure.md) - To understand the project organization
4. [**Project Status**](Project-Status.md) - To understand the current state of the project
5. [**Command Line Options**](Command-Line-Options.md) - To learn how to use the Pulsar command line interface

## Key Features

- **Template-Based Code Generation**: Flexible, maintainable code generation using templates as the source of truth
- **AOT Compatibility**: Full AOT support with proper attributes and trimming configuration for deployment in environments without JIT
- **Redis Integration**: Comprehensive Redis service with connection pooling, health monitoring, and error handling
- **Prometheus Metrics**: Built-in metrics for monitoring rule execution, cycle times, and Redis operations
- **Temporal Rule Support**: Circular buffer implementation for temporal rules with object value support
- **Rule Dependency Management**: Automatic dependency analysis and layer assignment
- **Performance Optimization**: Efficient rule evaluation with minimal overhead
- **Comprehensive Testing**: Extensive test suite for all components

## Usage

To generate a Beacon solution using the template-based approach:

```bash
dotnet run --project Pulsar.Compiler -- beacon --rules=rules.yaml --config=system_config.yaml --output=TestOutput/aot-beacon
```

To build the solution:

```bash
cd <output-dir>/Beacon
dotnet build
```

To create a standalone AOT-compatible executable:

```bash
cd <output-dir>/Beacon
dotnet publish Beacon.Runtime -c Release -r <runtime> --self-contained true -p:PublishAot=true
```

Where `<runtime>` can be `linux-x64`, `win-x64`, or `osx-x64` depending on your target platform.

## Contributing

Please refer to the [Testing Guide](Testing-Guide.md) for information on how to test your changes before submitting them.
