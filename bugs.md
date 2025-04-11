# Pulsar Bugs and Fixes

## 1. RedisService.cs Template Bug

### Issue
The RedisService.cs template in Pulsar.Compiler had an issue with pattern matching in the `SetOutputValuesAsync` method. The pattern matching was incorrectly handling numeric values, causing a compilation error:
```
An expression of type 'double' cannot be handled by a pattern of type 'bool'
```

### Fix
Updated the comment in the RedisService.cs template to clarify that numeric values are simply converted to strings:

```csharp
// Numeric values don't need special handling - convert to string
string valueStr = value.ToString();
```

### Impact
Without this fix, the Beacon application would fail to compile when the rules used numeric outputs.

### Verification
Successfully compiled a Beacon application from temperature_rules.yaml, which includes rules with numeric outputs.

## 2. ComparisonOperator Conversion Bug

### Issue
The `GenerateThresholdCondition` method in GenerationHelpers.cs was not properly converting the ComparisonOperator enum to the string operator format expected by the CheckThreshold method. This caused a runtime error:
```
Unsupported comparison operator: GreaterThan
```

### Fix
Updated the `GenerateThresholdCondition` method to properly convert the enum to string:

```csharp
public static string GenerateThresholdCondition(ThresholdOverTimeCondition threshold)
{
    // Convert the ComparisonOperator enum to the string operator format expected by CheckThreshold
    var op = threshold.ComparisonOperator switch
    {
        ComparisonOperator.GreaterThan => ">",
        ComparisonOperator.LessThan => "<",
        ComparisonOperator.GreaterThanOrEqual => ">=",
        ComparisonOperator.LessThanOrEqual => "<=",
        ComparisonOperator.EqualTo => "==",
        ComparisonOperator.NotEqualTo => "\!=",
        _ => throw new InvalidOperationException(
            $"Unknown operator: {threshold.ComparisonOperator}"
        ),
    };
    
    return $"CheckThreshold(\"{threshold.Sensor}\", {threshold.Threshold}, {threshold.Duration}, \"{op}\")";
}
```

### Impact
Without this fix, rules using ThresholdOverTime conditions would fail at runtime with an "Unsupported comparison operator" error.

### Verification
Successfully ran tests with temperature_rules.yaml, which includes rules with threshold conditions.

## 3. Rule Dependencies Bug

### Issue
The code was not properly handling rule dependencies when one rule referenced the output from another rule. This caused a KeyNotFoundException at runtime:
```
KeyNotFoundException: output:high_temperature
```

### Fix
Enhanced the `GenerateComparisonCondition` method in GenerationHelpers.cs to check both outputs and inputs dictionaries:

```csharp
// Special handling for sensor that might be an output from another rule
string sensorAccess;
if (comparison.Sensor.StartsWith("output:"))
{
    // For output sensors, try getting from outputs first, then inputs
    // Use unique variable names for TryGetValue to avoid conflicts
    string varName = $"outVal_{comparison.Sensor.Replace(":", "_")}";
    sensorAccess = $"(outputs.TryGetValue(\"{comparison.Sensor}\", out var {varName}) ? {varName} : " +
                  $"(inputs.ContainsKey(\"{comparison.Sensor}\") ? inputs[\"{comparison.Sensor}\"] : null))";
}
else
{
    // Regular input sensor
    sensorAccess = $"inputs[\"{comparison.Sensor}\"]";
}
```

Also updated the `RequiredSensors` property in RuleGroupGenerator.cs to include both inputs and outputs.

### Impact
Without this fix, rules that depended on outputs from other rules would fail with KeyNotFoundException at runtime.

### Verification
Successfully ran tests with temperature_rules.yaml, which includes rules with dependencies on other rules' outputs.

## 4. Variable Naming Conflict Bug

### Issue
The generated code had variable naming conflicts when accessing multiple output values, causing compilation errors.

### Fix
Updated the variable naming in the `GenerateComparisonCondition` method to use unique variable names for each `TryGetValue` operation:

```csharp
string varName = $"outVal_{comparison.Sensor.Replace(":", "_")}";
```

### Impact
Without this fix, rules that accessed multiple output values would have variable naming conflicts and fail to compile.

### Verification
Successfully compiled and ran tests with temperature_rules.yaml, which includes rules that access multiple output values.

## Summary
These fixes enable the end-to-end workflow from rule compilation to test execution to work correctly with the temperature_rules.yaml file, proving that the system can handle complex rules with dependencies.
