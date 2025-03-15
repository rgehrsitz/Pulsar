# Beacon Performance Tester

A comprehensive performance testing framework for Pulsar/Beacon rules processing.

## Overview

This framework provides tools for testing the performance, latency, and accuracy of Beacon rules processing in real-time scenarios. It consists of multiple components that work together to generate input data, monitor rule execution, and analyze results.

## Components

1. **Input Generator** - Generates simulated sensor data and writes to Redis with precise timing
2. **Output Monitor** - Tracks rule outputs and measures processing latency
3. **Performance Analyzer** - Collects and analyzes metrics like CPU usage, memory consumption, and throughput
4. **Test Orchestrator** - Coordinates test execution and collects results

## Getting Started

### Prerequisites

- .NET 9.0 SDK
- Redis server (default: localhost:6379)
- Compiled Pulsar/Beacon solution

### Building the Project

```bash
cd Beacon.PerformanceTester
dotnet build
```

### Running Tests

To run performance tests with the default scenario:

```bash
cd Beacon.PerformanceTester.InputGenerator
dotnet run
```

To specify a custom test scenario file:

```bash
dotnet run --scenarioFile="/path/to/your/test-scenarios.json"
```

## Test Scenarios

Test scenarios are defined in JSON format and include:

- Input sensor configurations (temperature, humidity, etc.)
- Data patterns (constant, random, sinusoidal, etc.)
- Update frequencies
- Expected outputs and acceptable latencies
- Test duration and other parameters

Example:

```json
{
  "Name": "Basic Latency Test",
  "Description": "Tests basic latency with constant values",
  "Inputs": [
    {
      "Key": "input:temperature",
      "PatternType": "Constant",
      "ConstantValue": 35
    }
  ],
  "ExpectedOutputs": [
    {
      "Key": "output:high_temperature",
      "Value": true,
      "MaxLatencyMs": 100
    }
  ]
}
```

## Data Patterns

The following patterns are supported for input data generation:

- **Constant** - Fixed value
- **Random** - Random values within a range
- **Stepped** - Values that increase/decrease at a fixed rate
- **Sinusoidal** - Oscillating values following a sine wave
- **Spike** - Periodic spikes in values
- **Sequence** - Custom sequence of values

## Extending the Framework

The framework is designed to be extensible:

1. Add new data patterns by implementing the `IPatternGenerator` interface
2. Create custom test scenarios for specific use cases
3. Add new output monitors to track different metrics

## Test Categories

The included test scenarios cover various aspects of Beacon performance:

- **Latency** - Measures processing time from input to output
- **Throughput** - Tests performance under high data volume
- **Temporal Rules** - Tests time-based processing with sliding windows
- **Rule Dependencies** - Tests complex rule relationships
- **Long-Running** - Tests stability over extended periods

## License

MIT