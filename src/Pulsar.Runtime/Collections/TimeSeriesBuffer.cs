using System;
using System.Collections.Generic;
using System.Linq;
using Pulsar.Runtime.Services;
using Serilog;

namespace Pulsar.Runtime.Collections;

/// <summary>
/// A fixed-size circular buffer optimized for time-series data
/// </summary>
public class TimeSeriesBuffer
{
    private readonly (DateTime Timestamp, double Value)[] _buffer;
    private readonly ILogger _logger;
    private readonly IMetricsService? _metrics;
    private readonly string _dataSource;
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
    /// <param name="dataSource">The name of the data source this buffer is for</param>
    /// <param name="capacity">The maximum number of elements the buffer can hold</param>
    /// <param name="logger">Optional logger instance. If not provided, will use Log.Logger</param>
    /// <param name="metrics">Optional metrics service instance</param>
    public TimeSeriesBuffer(
        string dataSource,
        int capacity,
        ILogger? logger = null,
        IMetricsService? metrics = null
    )
    {
        if (capacity <= 0)
            throw new ArgumentException("Capacity must be greater than 0", nameof(capacity));

        _buffer = new (DateTime, double)[capacity];
        _logger = logger?.ForContext<TimeSeriesBuffer>() ?? Log.Logger;
        _metrics = metrics;
        _dataSource = dataSource;
        _start = 0;
        _count = 0;
        _oldestTimestamp = DateTime.MinValue;
        _newestTimestamp = DateTime.MinValue;

        _logger.Debug(
            "Created new time series buffer for {DataSource} with capacity {Capacity}",
            dataSource,
            capacity
        );
    }

    /// <summary>
    /// Adds a value to the buffer with the current UTC timestamp
    /// </summary>
    /// <param name="value">The value to add</param>
    public void Add(double value)
    {
        Add(DateTime.UtcNow, value);
    }

    /// <summary>
    /// Adds a value to the buffer with the specified timestamp
    /// </summary>
    /// <param name="timestamp">The timestamp of the value</param>
    /// <param name="value">The value to add</param>
    public void Add(DateTime timestamp, double value)
    {
        if (_count > 0 && timestamp < _newestTimestamp)
        {
            _logger.Warning(
                "Out of order timestamp detected for {DataSource}. New: {NewTimestamp}, Last: {LastTimestamp}",
                _dataSource,
                timestamp,
                _newestTimestamp
            );
            return;
        }

        var index = (_start + _count) % Capacity;
        _buffer[index] = (timestamp, value);

        if (_count < Capacity)
        {
            _count++;
            if (_count == 1)
                _oldestTimestamp = timestamp;
        }
        else
        {
            _start = (_start + 1) % Capacity;
            _oldestTimestamp = _buffer[_start].Timestamp;
            _metrics?.RecordTimeSeriesOverflow(_dataSource);
        }

        _newestTimestamp = timestamp;
        _metrics?.RecordTimeSeriesBufferSize(_dataSource, _count);
    }

    /// <summary>
    /// Gets all values within the specified time window
    /// </summary>
    /// <param name="duration">The duration of the time window</param>
    /// <returns>An array of timestamp-value pairs within the window</returns>
    public (DateTime Timestamp, double Value)[] GetTimeWindow(TimeSpan duration)
    {
        if (_count == 0)
            return Array.Empty<(DateTime, double)>();

        var cutoff = _newestTimestamp - duration;
        if (cutoff > _newestTimestamp)
            return Array.Empty<(DateTime, double)>();

        var values = new List<(DateTime, double)>();
        var current = _start;

        for (var i = 0; i < _count; i++)
        {
            var (timestamp, value) = _buffer[current];
            if (timestamp >= cutoff)
                values.Add((timestamp, value));

            current = (current + 1) % Capacity;
        }

        return values.ToArray();
    }

    /// <summary>
    /// Gets all values in the buffer
    /// </summary>
    /// <returns>An array of all timestamp-value pairs in chronological order</returns>
    public (DateTime Timestamp, double Value)[] GetAll()
    {
        if (_count == 0)
            return Array.Empty<(DateTime, double)>();

        var values = new List<(DateTime, double)>();
        var current = _start;

        for (var i = 0; i < _count; i++)
        {
            var (timestamp, value) = _buffer[current];
            values.Add((timestamp, value));
            current = (current + 1) % Capacity;
        }

        return values.ToArray();
    }

    /// <summary>
    /// Clears all values from the buffer
    /// </summary>
    public void Clear()
    {
        _start = 0;
        _count = 0;
        _oldestTimestamp = DateTime.MinValue;
        _newestTimestamp = DateTime.MinValue;
        _logger.Debug("Cleared buffer for {DataSource}", _dataSource);
    }
}
