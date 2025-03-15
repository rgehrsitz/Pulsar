# Pulsar/Beacon Automated Testing Framework

This framework provides an automated end-to-end testing solution for Pulsar/Beacon. It verifies the complete flow from YAML rule definition to final rule execution with Redis integration.

## Framework Components

1. **Test Rules**: YAML files containing rule definitions
2. **System Config**: Configuration for valid sensors and Redis connection
3. **Test Scenarios**: JSON definitions of test inputs and expected outputs
4. **PulsarBeaconTester**: C# class that orchestrates the testing pipeline
5. **Simplified Test Script**: Bash script for running mock-based tests without the Beacon executable

## Testing Approaches

The framework provides two testing approaches:

1. **Full End-to-End Testing**: Uses PulsarBeaconTester.cs to compile rules, build and run the Beacon executable, and validate results through Redis.

2. **Simplified Testing**: Uses run-simplified-tests.sh to validate rule compilation and code generation, then uses mock rule implementations to verify logic without running the full Beacon runtime.

## How It Works

### Full End-to-End Testing

1. Takes YAML rule definitions and compiles them using the Pulsar compiler
2. Builds the generated Beacon solution
3. Runs the Beacon executable with proper configuration
4. Interacts with Redis to provide test input values
5. Verifies the output values in Redis match expected results
6. Reports pass/fail status for each test scenario

### Simplified Testing

1. Compiles YAML rules using the Pulsar compiler
2. Verifies generated C# code structure
3. Uses mock rule implementations to simulate rule execution
4. Validates expected outputs against mock rule results
5. Reports pass/fail status for each test scenario

## Running Tests

### Prerequisites
- Redis running locally (default: localhost:6379)
- .NET SDK installed
- Pulsar solution built

### Running Full End-to-End Tests

From the solution directory:

```bash
dotnet test --filter "FullyQualifiedName=Pulsar.TestAutomation.PulsarBeaconEndToEndTests.RunBasicRuleTests"
```

### Running Simplified Tests

From the TestAutomation directory:

```bash
./run-simplified-tests.sh
```

## Test Scenarios

Test scenarios are defined in JSON and support:

### Basic Features
- Static input values
- Time-series input sequences for temporal rules
- Expected output values with optional tolerance
- Boolean, numeric, and string comparison

### Advanced Features
- **Pre-Set Outputs**: Set outputs before rule execution to test rule dependencies
- **Rule Dependencies**: Test rules that depend on outputs from other rules
- **Negative Tests**: Test conditions that should not trigger
- **Complex Temporal Patterns**: Test temporal rules with complex patterns
- **Alternative Execution Paths**: Test rules with OR conditions

### Example Scenarios

Basic scenario:
```json
{
  "name": "SimpleTemperatureTestHigh",
  "description": "Tests high temperature (above threshold)",
  "inputs": {
    "input:temperature": 35
  },
  "expectedOutputs": {
    "output:high_temperature": true
  }
}
```

Temporal sequence scenario:
```json
{
  "name": "TemperatureRisingPatternTest",
  "description": "Tests detection of rising temperature",
  "inputSequence": [
    { "input:temperature": 20, "delayMs": 100 },
    { "input:temperature": 25, "delayMs": 100 },
    { "input:temperature": 30, "delayMs": 100 }
  ],
  "expectedOutputs": {
    "output:temperature_rising": true
  }
}
```

Dependency testing scenario:
```json
{
  "name": "LayeredDependencyTest",
  "description": "Tests rule depending on outputs from other rules",
  "preSetOutputs": {
    "output:high_temperature": true,
    "output:heat_alert": true
  },
  "inputs": {
    "input:temperature": 82,
    "input:humidity": 75
  },
  "expectedOutputs": {
    "output:emergency_alert": true
  }
}
```

## Adding New Tests

To add new tests:

1. Add new rule definitions to test-rules.yaml
2. Add any new sensors/outputs to system_config.yaml
3. Add test scenarios to test-scenarios.json
4. For simplified testing, update the evaluate_rule function in run-simplified-tests.sh to handle new rules

## Best Practices

- Keep rules small and focused on specific functionality
- Test both normal and edge cases
- Test rule dependencies comprehensively
- For temporal rules, use proper sequence timing
- Include negative test cases (conditions that should not trigger)
- Set realistic tolerance values for floating-point comparisons
- Verify both boolean and numeric outputs
- Test all execution paths in complex rules
- For rules with OR conditions, test each path separately

## Extending the Framework

Consider these enhancements for further development:

- Automatic test discovery based on rule names
- Performance testing for rule execution
- Visualization of rule dependencies
- Integration with CI/CD pipelines
- Support for testing rule layers and execution order
- Extended temporal pattern testing