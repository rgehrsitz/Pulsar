using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Pulsar.Runtime.Collections;
using Serilog;

namespace Pulsar.Runtime.Services;

/// <summary>
/// Service for managing time series data buffers
/// </summary>
public class TimeSeriesService
{
    private readonly ConcurrentDictionary<string, TimeSeriesBuffer> _buffers;
    private readonly ILogger _logger;
    private readonly int _defaultCapacity;

    public TimeSeriesService(ILogger logger, int defaultCapacity = 1000)
    {
        _buffers = new ConcurrentDictionary<string, TimeSeriesBuffer>();
        _logger = logger.ForContext<TimeSeriesService>();
        _defaultCapacity = defaultCapacity;
    }

    /// <summary>
    /// Updates the time series data for a specific data source
    /// </summary>
    /// <param name="dataSource">The name of the data source</param>
    /// <param name="value">The current value</param>
    /// <param name="timestamp">Optional timestamp for the value. If not provided, UTC now is used.</param>
    public void Update(string dataSource, double value, DateTime? timestamp = null)
    {
        var buffer = _buffers.GetOrAdd(dataSource, _ => new TimeSeriesBuffer(_defaultCapacity));
        buffer.Add(timestamp ?? DateTime.UtcNow, value);
    }

    /// <summary>
    /// Gets values within a time window for a specific data source
    /// </summary>
    /// <param name="dataSource">The name of the data source</param>
    /// <param name="duration">The duration of the time window</param>
    /// <returns>An array of timestamp-value pairs within the window</returns>
    public (DateTime Timestamp, double Value)[] GetTimeWindow(string dataSource, TimeSpan duration)
    {
        if (!_buffers.TryGetValue(dataSource, out var buffer))
        {
            _logger.Warning("No buffer found for data source: {DataSource}", dataSource);
            return Array.Empty<(DateTime, double)>();
        }

        return buffer.GetTimeWindow(duration);
    }

    /// <summary>
    /// Clears all data for a specific data source
    /// </summary>
    /// <param name="dataSource">The name of the data source to clear</param>
    public void Clear(string dataSource)
    {
        if (_buffers.TryGetValue(dataSource, out var buffer))
        {
            buffer.Clear();
        }
    }

    /// <summary>
    /// Clears all data from all buffers
    /// </summary>
    public void ClearAll()
    {
        foreach (var buffer in _buffers.Values)
        {
            buffer.Clear();
        }
    }
}
