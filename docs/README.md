# Pulsar/Beacon Documentation <img src="pulsar.png" height="75px">

[![License](https://img.shields.io/badge/License-MIT-blue)](#license)

## Overview

This directory contains the documentation for the Pulsar/Beacon project, a high-performance, AOT-compatible rules evaluation system. The documentation has been consolidated into focused, comprehensive documents that cover all aspects of the system.

## Documentation Index

### Core Documentation

1. [**Project Status**](Project-Status.md) - Current status of the project, recent fixes, and next steps
2. [**AOT Implementation**](AOT-Implementation.md) - Details of the AOT (Ahead-of-Time) compilation implementation
3. [**Rules Engine**](Rules-Engine.md) - Overview of the rules engine, including rule definitions, conditions, actions, and execution

### Technical Components

4. [**Redis Integration**](Redis-Integration.md) - Redis integration components, configuration, and best practices
5. [**Temporal Buffer**](Temporal-Buffer.md) - Implementation of the temporal buffer for historical data storage and evaluation

### User Guides

6. [**End-to-End Guide**](End-to-End-Guide.md) - Complete walkthrough from creating YAML rules to running a Beacon application

### Development and Testing

7. [**Testing Guide**](Testing-Guide.md) - Comprehensive guide to testing the Pulsar/Beacon system

## Getting Started

If you're new to the Pulsar/Beacon project, we recommend starting with the following documents:

1. [**End-to-End Guide**](End-to-End-Guide.md) - Complete walkthrough for new users
2. [**Rules Engine**](Rules-Engine.md) - To learn about rule definitions and capabilities
3. [**Project Status**](Project-Status.md) - To understand the current state of the project

## Key Features

- **AOT Compatibility**: Full AOT support with proper attributes and trimming configuration
- **Redis Integration**: Comprehensive Redis service with connection pooling, health monitoring, and error handling
- **Temporal Rule Support**: Circular buffer implementation for temporal rules with object value support
- **Rule Dependency Management**: Automatic dependency analysis and layer assignment
- **Performance Optimization**: Efficient rule evaluation with minimal overhead
- **Comprehensive Testing**: Extensive test suite for all components

## Usage

To generate a Beacon solution:

```bash
dotnet run --project Pulsar.Compiler -- beacon --rules=rules.yaml --config=system_config.yaml --output=TestOutput/aot-beacon
```

To build the solution:

```bash
cd <output-dir>/Beacon
dotnet build
```

To create a standalone executable:

```bash
cd <output-dir>/Beacon
dotnet publish -c Release -r <runtime> --self-contained true
```

## Contributing

Please refer to the [Testing Guide](Testing-Guide.md) for information on how to test your changes before submitting them.
