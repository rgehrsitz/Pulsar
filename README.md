# Pulsar Rules Engine

A high-performance rules engine for processing sensor data and generating alerts.

## Project Structure

- `src/Pulsar.Core`: Core interfaces and models
- `src/Pulsar.RuleDefinition`: Rule definition and validation
- `src/Pulsar.Compiler`: Rule compiler and code generation
- `src/Pulsar.Runtime`: Runtime engine and execution

## Building

```bash
dotnet build
```

## Testing

```bash
dotnet test
```

## Running Benchmarks

```bash
cd tools/Pulsar.Benchmarks
dotnet run -c Release
```
