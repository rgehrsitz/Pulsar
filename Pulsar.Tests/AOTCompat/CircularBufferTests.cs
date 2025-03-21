// Add missing namespaces if any
// For numeric operations that might need this
using Microsoft.Extensions.Logging;
using Pulsar.Tests.TestUtilities;
using Xunit.Abstractions;

namespace Pulsar.Tests.AOTCompat
{
    [Trait("Category", "AOTCompatibility")]
    public class CircularBufferTests(ITestOutputHelper output)
    {
        private readonly ILogger _logger = LoggingConfig.GetLoggerForTests(output);

        [Fact]
        public void CircularBuffer_WithObjectValues_WorksCorrectly()
        {
            // Create a circular buffer with a capacity of 5
            var buffer = new SimpleCircularBuffer<object>(5);

            // Add some values
            buffer.Add("value1");
            buffer.Add("value2");
            buffer.Add("value3");

            // Check count
            Assert.Equal(3, buffer.Count);

            // Get values
            Assert.Equal("value1", buffer.GetAtIndex(0));
            Assert.Equal("value2", buffer.GetAtIndex(1));
            Assert.Equal("value3", buffer.GetAtIndex(2));

            // Add more values to trigger wrapping
            buffer.Add("value4");
            buffer.Add("value5");
            buffer.Add("value6");

            // Check count - should be at capacity
            Assert.Equal(5, buffer.Count);

            // Check values after wrapping
            Assert.Equal("value2", buffer.GetAtIndex(0));
            Assert.Equal("value3", buffer.GetAtIndex(1));
            Assert.Equal("value4", buffer.GetAtIndex(2));
            Assert.Equal("value5", buffer.GetAtIndex(3));
            Assert.Equal("value6", buffer.GetAtIndex(4));

            output.WriteLine("Circular buffer with object values works correctly");
        }

        [Fact]
        public void CircularBuffer_WithNumericCalculations_WorksCorrectly()
        {
            // Create a circular buffer with a capacity of 10
            var buffer = new SimpleCircularBuffer<int>(10);

            // Add increasing values
            for (int i = 1; i <= 15; i++)
            {
                buffer.Add(i);
            }

            // Check count - should be at capacity
            Assert.Equal(10, buffer.Count);

            // Calculate average (should be average of 6 through 15)
            var average = buffer.Average();
            Assert.Equal(10.5, average);

            // Calculate max (should be 15)
            var max = buffer.Max();
            Assert.Equal(15, max);

            // Calculate min (should be 6)
            var min = buffer.Min();
            Assert.Equal(6, min);

            // Check if increasing
            Assert.True(buffer.IsIncreasing());

            // Check if value increased by at least 5
            Assert.True(buffer.IncreasedBy(5));

            // Check if not increased by more than the actual increase
            Assert.False(buffer.IncreasedBy(20));

            output.WriteLine("Circular buffer with numeric calculations works correctly");
        }

        [Fact]
        public void CircularBuffer_WithTimestampedValues_WorksCorrectly()
        {
            // Create a circular buffer with timestamps
            var buffer = new TimestampedCircularBuffer<double>(10);

            // Add values with timestamps
            var now = DateTime.UtcNow;
            buffer.Add(10.0, now.AddMinutes(-10));
            buffer.Add(20.0, now.AddMinutes(-8));
            buffer.Add(30.0, now.AddMinutes(-5));
            buffer.Add(40.0, now.AddMinutes(-2));

            // Get values from specific time window
            var recentValues = buffer.GetValuesAfter(now.AddMinutes(-6));
            Assert.Equal(2, recentValues.Count);
            Assert.Equal(30.0, recentValues[0]);
            Assert.Equal(40.0, recentValues[1]);

            // Check if increasing in timespan
            Assert.True(buffer.IsIncreasingInTimespan(TimeSpan.FromMinutes(6)));

            output.WriteLine("Circular buffer with timestamped values works correctly");
        }

        // Simple circular buffer implementation for testing AOT compatibility
        private class SimpleCircularBuffer<T>(int capacity)
        {
            private readonly T[] _buffer = new T[capacity];
            private int _start = 0;
            private int _count = 0;

            public int Count => _count;

            public void Add(T value)
            {
                if (_count < capacity)
                {
                    // Buffer not full yet
                    _buffer[(_start + _count) % capacity] = value;
                    _count++;
                }
                else
                {
                    // Buffer full, overwrite oldest value
                    _buffer[_start] = value;
                    _start = (_start + 1) % capacity;
                }
            }

            public T GetAtIndex(int index)
            {
                if (index < 0 || index >= _count)
                    throw new ArgumentOutOfRangeException(nameof(index));

                return _buffer[(_start + index) % capacity];
            }

            public double Average()
            {
                if (_count == 0)
                    return 0;

                double sum = 0;
                for (int i = 0; i < _count; i++)
                {
                    try
                    {
                        sum += Convert.ToDouble(GetAtIndex(i));
                    }
                    catch (InvalidCastException ex)
                    {
                        throw new InvalidOperationException(
                            $"Cannot convert value at index {i} to double for average calculation",
                            ex
                        );
                    }
                }
                return sum / _count;
            }

            public double Max()
            {
                if (_count == 0)
                    throw new InvalidOperationException("Buffer is empty");

                double max = 0;
                bool initialized = false;

                for (int i = 0; i < _count; i++)
                {
                    try
                    {
                        double val = Convert.ToDouble(GetAtIndex(i));
                        if (!initialized || val > max)
                        {
                            max = val;
                            initialized = true;
                        }
                    }
                    catch (InvalidCastException ex)
                    {
                        throw new InvalidOperationException(
                            $"Cannot convert value at index {i} to double for max calculation",
                            ex
                        );
                    }
                }

                if (!initialized)
                    throw new InvalidOperationException("Could not find any convertible values");

                return max;
            }

            public double Min()
            {
                if (_count == 0)
                    throw new InvalidOperationException("Buffer is empty");

                double min = 0;
                bool initialized = false;

                for (int i = 0; i < _count; i++)
                {
                    try
                    {
                        double val = Convert.ToDouble(GetAtIndex(i));
                        if (!initialized || val < min)
                        {
                            min = val;
                            initialized = true;
                        }
                    }
                    catch (InvalidCastException ex)
                    {
                        throw new InvalidOperationException(
                            $"Cannot convert value at index {i} to double for min calculation",
                            ex
                        );
                    }
                }

                if (!initialized)
                    throw new InvalidOperationException("Could not find any convertible values");

                return min;
            }

            public bool IsIncreasing()
            {
                if (_count < 2)
                    return true;

                for (int i = 0; i < _count - 1; i++)
                {
                    if (Convert.ToDouble(GetAtIndex(i)) > Convert.ToDouble(GetAtIndex(i + 1)))
                        return false;
                }
                return true;
            }

            public bool IncreasedBy(double threshold)
            {
                if (_count < 2)
                    return false;

                double first = Convert.ToDouble(GetAtIndex(0));
                double last = Convert.ToDouble(GetAtIndex(_count - 1));

                return (last - first) >= threshold;
            }
        }

        private class TimestampedCircularBuffer<T>(int capacity)
        {
            private readonly (T Value, DateTime Timestamp)[] _buffer = new (T, DateTime)[capacity];
            private int _start = 0;
            private int _count = 0;

            public void Add(T value, DateTime timestamp)
            {
                if (_count < capacity)
                {
                    // Buffer not full yet
                    _buffer[(_start + _count) % capacity] = (value, timestamp);
                    _count++;
                }
                else
                {
                    // Buffer full, overwrite oldest value
                    _buffer[_start] = (value, timestamp);
                    _start = (_start + 1) % capacity;
                }
            }

            public List<T> GetValuesAfter(DateTime timestamp)
            {
                var result = new List<T>();

                for (int i = 0; i < _count; i++)
                {
                    var item = _buffer[(_start + i) % capacity];
                    if (item.Timestamp > timestamp)
                    {
                        result.Add(item.Value);
                    }
                }

                return result;
            }

            public bool IsIncreasingInTimespan(TimeSpan timespan)
            {
                var now = DateTime.UtcNow;
                var cutoff = now - timespan;

                var recentValues = new List<(T Value, DateTime Timestamp)>();

                for (int i = 0; i < _count; i++)
                {
                    var item = _buffer[(_start + i) % capacity];
                    if (item.Timestamp > cutoff)
                    {
                        recentValues.Add(item);
                    }
                }

                if (recentValues.Count < 2)
                    return true;

                for (int i = 0; i < recentValues.Count - 1; i++)
                {
                    if (
                        Convert.ToDouble(recentValues[i].Value)
                        > Convert.ToDouble(recentValues[i + 1].Value)
                    )
                        return false;
                }

                return true;
            }
        }
    }
}
