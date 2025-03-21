# Temporal Buffer Implementation

## Overview

The Temporal Buffer is a critical component of the Pulsar/Beacon system that enables temporal rule evaluation by storing historical sensor values. The implementation is located in the Pulsar.Compiler/Config/Templates/Runtime/Buffers directory and is included in the generated Beacon application. This document explains the implementation, configuration, and usage of the circular buffer system, which now supports generic object values rather than just numeric values.

## Temporal Modes

The system supports two distinct temporal evaluation modes:

### Strict Discrete Mode (Default)

- You only trust explicit data points.
- If you have no new reading in the last 200 ms, you assume nothing. The sensor might have changed.
- The ring buffer logic is correct for that scenario—where "continuously above threshold" is interpreted as "every data point that arrived in that window was above threshold, with no data point below threshold in between."

### Extended Last-Known Reading Mode

- Once you get a reading above threshold at time T, you treat the sensor as still at or above threshold for any subsequent times until you see a reading that contradicts it.
- In effect, you "fill in" all the times between T and now with that last value. If a new data point arrives that is below threshold, you reset.
- This approach is common if you have slow sensor updates but assume "no news is good news."

## Buffer Capacity Calculation

A ring buffer (or circular buffer) has a fixed capacity—once you fill it, adding a new item overwrites the oldest. To ensure your buffer always contains at least the last X milliseconds of data:

1. **Determine the data sampling rate**
   - If you fetch from Redis once every P milliseconds, your sampling frequency is 1000/P samples per second.
   - For example, if you fetch data every 100 ms, that's 10 samples/second.

2. **Identify your largest needed time window**
   - For instance, if your largest threshold-based check or time window is 5 seconds, you want to be able to look at the last 5 seconds of data.

3. **Calculate capacity**
   - Multiply (sampling frequency) × (largest time window) + overhead.
   - Example: 10 samples/second * 5 seconds = 50 samples.
   - Add 10–20% overhead to guard against timing variations: 50 * 1.2 = 60 samples.

4. **Use that capacity when instantiating each CircularBuffer**
   - E.g., new CircularBuffer(60).

## Implementation Details

### TimestampedValue Struct

The `TimestampedValue` struct represents a single value with its timestamp:

```csharp
public readonly struct TimestampedValue
{
    public readonly DateTime Timestamp { get; init; }
    public readonly object Value { get; init; }

    public TimestampedValue(DateTime timestamp, object value)
    {
        Timestamp = timestamp;
        Value = value;
    }
}
```

### CircularBuffer Class

The `CircularBuffer` class implements a fixed-size ring buffer for a single sensor's values:

```csharp
public class CircularBuffer
{
    private readonly TimestampedValue[] _buffer;
    private int _head;
    private int _count;
    private readonly ReaderWriterLockSlim _lock = new();
    private readonly IDateTimeProvider _dateTimeProvider;

    public CircularBuffer(int capacity, IDateTimeProvider dateTimeProvider)
    {
        // Implementation details...
    }

    public void Add(object value, DateTime timestamp)
    {
        // Implementation details...
    }

    public IEnumerable<TimestampedValue> GetValues(TimeSpan duration, bool includeOlder = false)
    {
        // Implementation details...
    }

    public bool IsAboveThresholdForDuration(double threshold, TimeSpan duration, bool extendLastKnown = false)
    {
        // Implementation details...
    }

    public bool IsBelowThresholdForDuration(double threshold, TimeSpan duration, bool extendLastKnown = false)
    {
        // Implementation details...
    }
}
```

### RingBufferManager Class

The `RingBufferManager` class manages ring buffers for multiple sensors:

```csharp
public class RingBufferManager : IDisposable
{
    private readonly ConcurrentDictionary<string, CircularBuffer> _buffers;
    private readonly int _capacity;
    private readonly IDateTimeProvider _dateTimeProvider;

    public RingBufferManager(int capacity = 100, IDateTimeProvider? dateTimeProvider = null)
    {
        // Implementation details...
    }

    public void UpdateBuffer(string sensor, object value, DateTime timestamp)
    {
        // Implementation details...
    }

    public void UpdateBuffers(Dictionary<string, object> currentValues)
    {
        // Implementation details...
    }

    public bool IsAboveThresholdForDuration(string sensor, double threshold, TimeSpan duration, bool extendLastKnown = false)
    {
        // Implementation details...
    }

    public bool IsBelowThresholdForDuration(string sensor, double threshold, TimeSpan duration, bool extendLastKnown = false)
    {
        // Implementation details...
    }

    public void Clear()
    {
        // Implementation details...
    }

    public void Dispose()
    {
        // Implementation details...
    }
}
```

## Key Features

1. **Object Value Support**
   - The buffer now supports any object value type, not just doubles
   - Proper conversion to double for threshold comparisons
   - Thread-safe operations for object values

2. **Time-Based Filtering**
   - GetValues method filters values based on a time window
   - Support for both strict and extended last-known modes
   - Proper handling of timestamps for accurate temporal evaluation

3. **Efficient Memory Usage**
   - Fixed-size buffer with oldest value overwriting
   - Only sensors that need historical values are cached
   - Configurable capacity based on application needs

4. **Thread Safety**
   - ReaderWriterLockSlim for efficient concurrent access
   - Thread-safe buffer operations
   - Concurrent dictionary for managing multiple buffers

## Usage Examples

### Calculating Rate of Change

```csharp
// Rule that calculates rate of change
var rule = new RuleDefinition
{
    Name = "RateOfChangeRule",
    Description = "Calculates rate of change for a sensor value",
    Conditions = new ConditionGroup
    {
        All = new List<ConditionDefinition>
        {
            new ExpressionCondition
            {
                Expression = "input:sensor > 0"
            }
        }
    },
    Actions = new List<ActionDefinition>
    {
        new SetValueAction
        {
            Key = "buffer:sensor_value",
            ValueExpression = "input:sensor"
        },
        new SetValueAction
        {
            Key = "buffer:timestamp",
            ValueExpression = "input:timestamp"
        },
        new SetValueAction
        {
            Key = "output:rate_of_change",
            ValueExpression = "(input:sensor - buffer:sensor_value[-1]) / ((input:timestamp - buffer:timestamp[-1]) / 1000.0)"
        }
    }
};
```

### Checking if Value is Above Threshold for Duration

```csharp
// Check if temperature is above 80 degrees for 5 minutes
bool isOverheating = bufferManager.IsAboveThresholdForDuration("temperature", 80.0, TimeSpan.FromMinutes(5));
```

### Using Extended Last-Known Mode

```csharp
// Check if pressure is below 30 for 10 minutes, using extended last-known mode
bool isPressureLow = bufferManager.IsBelowThresholdForDuration("pressure", 30.0, TimeSpan.FromMinutes(10), true);
```

## Best Practices

1. **Determine your sampling rate and largest needed window upfront**
   - Calculate buffer capacity based on these values
   - Add overhead to account for timing variations

2. **Choose the appropriate temporal mode**
   - Use strict discrete mode for high-frequency sensors where every data point matters
   - Use extended last-known mode for slow-updating sensors where gaps are expected

3. **Optimize memory usage**
   - Only cache sensors that need historical values
   - Configure appropriate buffer capacity for each sensor

4. **Handle thread safety**
   - Use the provided thread-safe methods
   - Avoid direct buffer manipulation

5. **Consider performance implications**
   - Balance buffer size with memory usage
   - Use appropriate time windows for temporal rules
