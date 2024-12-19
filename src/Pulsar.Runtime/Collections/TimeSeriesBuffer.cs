using System;
using System.Collections.Generic;
using System.Linq;
using Serilog;

namespace Pulsar.Runtime.Collections;

/// <summary>
/// A fixed-size circular buffer optimized for time-series data
/// </summary>
public class TimeSeriesBuffer
{
    private readonly (DateTime Timestamp, double Value)[] _buffer;
    private readonly ILogger _logger;
    private int _start;
    private int _count;
    private DateTime _oldestTimestamp;
    private DateTime _newestTimestamp;

    /// <summary>
    /// Gets the capacity of the buffer
    /// </summary>
    public int Capacity => _buffer.Length;

    /// <summary>
    /// Gets the current number of elements in the buffer
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Gets whether the buffer is full
    /// </summary>
    public bool IsFull => _count == Capacity;

    /// <summary>
    /// Gets the timestamp of the oldest value in the buffer
    /// </summary>
    public DateTime OldestTimestamp => _oldestTimestamp;

    /// <summary>
    /// Gets the timestamp of the newest value in the buffer
    /// </summary>
    public DateTime NewestTimestamp => _newestTimestamp;

    /// <summary>
    /// Creates a new time series buffer with the specified capacity
    /// </summary>
    /// <param name="capacity">The maximum number of elements the buffer can hold</param>
    /// <param name="logger">Optional logger instance. If not provided, will use Log.Logger</param>
    public TimeSeriesBuffer(int capacity, ILogger? logger = null)
    {
        if (capacity <= 0)
            throw new ArgumentException("Capacity must be greater than 0", nameof(capacity));

        _buffer = new (DateTime, double)[capacity];
        _start = 0;
        _count = 0;
        _oldestTimestamp = DateTime.MaxValue;
        _newestTimestamp = DateTime.MinValue;
        _logger = (logger ?? Log.Logger).ForContext<TimeSeriesBuffer>();

        _logger.Debug("Created new time series buffer with capacity {Capacity}", capacity);
    }

    /// <summary>
    /// Adds a value to the buffer with the current UTC timestamp
    /// </summary>
    /// <param name="value">The value to add</param>
    public void Add(double value) => Add(DateTime.UtcNow, value);

    /// <summary>
    /// Adds a value to the buffer with the specified timestamp
    /// </summary>
    /// <param name="timestamp">The timestamp of the value</param>
    /// <param name="value">The value to add</param>
    public void Add(DateTime timestamp, double value)
    {
        var index = (_start + _count) % Capacity;
        _buffer[index] = (timestamp, value);

        if (_count < Capacity)
        {
            _count++;
            _logger.Debug("Added value {Value} at {Timestamp}. Buffer count: {Count}/{Capacity}", 
                value, timestamp, _count, Capacity);
        }
        else
        {
            _start = (_start + 1) % Capacity;
            // Update oldest timestamp when we overwrite the oldest value
            _oldestTimestamp = _buffer[_start].Timestamp;
            _logger.Debug("Buffer full, overwrote oldest value. New start index: {StartIndex}, new oldest timestamp: {OldestTimestamp}", 
                _start, _oldestTimestamp);
        }

        // Update timestamp bounds
        if (timestamp < _oldestTimestamp || _count == 1)
        {
            _oldestTimestamp = timestamp;
            _logger.Debug("Updated oldest timestamp to {Timestamp}", timestamp);
        }
        if (timestamp > _newestTimestamp || _count == 1)
        {
            _newestTimestamp = timestamp;
            _logger.Debug("Updated newest timestamp to {Timestamp}", timestamp);
        }
    }

    /// <summary>
    /// Gets all values within the specified time window
    /// </summary>
    /// <param name="duration">The duration of the time window</param>
    /// <returns>An array of timestamp-value pairs within the window</returns>
    public (DateTime Timestamp, double Value)[] GetTimeWindow(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
            throw new ArgumentException("Duration must be greater than 0", nameof(duration));

        if (_count == 0)
        {
            _logger.Debug("Buffer is empty, returning empty array");
            return Array.Empty<(DateTime, double)>();
        }

        var cutoff = DateTime.UtcNow - duration;
        _logger.Debug("Getting values after {Cutoff} (window: {Duration})", cutoff, duration);

        var result = new List<(DateTime, double)>();
        for (int i = 0; i < _count; i++)
        {
            var index = (_start + i) % Capacity;
            var (timestamp, value) = _buffer[index];
            if (timestamp >= cutoff)
            {
                result.Add((timestamp, value));
            }
        }

        _logger.Debug("Found {Count} values in time window", result.Count);
        return result.ToArray();
    }

    /// <summary>
    /// Gets all values in the buffer
    /// </summary>
    /// <returns>An array of all timestamp-value pairs in chronological order</returns>
    public (DateTime Timestamp, double Value)[] GetAll()
    {
        _logger.Debug("Getting all values from buffer. Count: {Count}", _count);
        var result = new (DateTime, double)[_count];
        for (int i = 0; i < _count; i++)
        {
            var index = (_start + i) % Capacity;
            result[i] = _buffer[index];
        }
        return result;
    }

    /// <summary>
    /// Clears all values from the buffer
    /// </summary>
    public void Clear()
    {
        _logger.Debug("Clearing buffer. Previous count: {Count}", _count);
        Array.Clear(_buffer, 0, _buffer.Length);
        _start = 0;
        _count = 0;
        _oldestTimestamp = DateTime.MaxValue;
        _newestTimestamp = DateTime.MinValue;
    }
}
