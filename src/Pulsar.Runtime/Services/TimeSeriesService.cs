using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Pulsar.Core.Collections;
using Pulsar.Core.Services; // Ensure using Core's interface
using Serilog;

namespace Pulsar.Runtime.Services;

/// <summary>
/// Service for managing time series data buffers
/// </summary>
public class TimeSeriesService
{
    private readonly ConcurrentDictionary<string, TimeSeriesBuffer> _buffers;
    private readonly ILogger _logger;
    private readonly Core.Services.IMetricsService _metrics; // Use Core's interface
    private readonly int _defaultCapacity;

    public TimeSeriesService(
        ILogger logger,
        Core.Services.IMetricsService metrics, // Use Core's interface
        int defaultCapacity = 1000
    )
    {
        _buffers = new ConcurrentDictionary<string, TimeSeriesBuffer>();
        _logger = logger.ForContext<TimeSeriesService>();
        _metrics = metrics;
        _defaultCapacity = defaultCapacity;
        _logger.Debug(
            "Created new time series service with default capacity {DefaultCapacity}",
            defaultCapacity
        );
    }

    /// <summary>
    /// Updates the time series data for a specific data source
    /// </summary>
    /// <param name="dataSource">The name of the data source</param>
    /// <param name="value">The current value</param>
    /// <param name="timestamp">Optional timestamp for the value. If not provided, UTC now is used.</param>
    public void Update(string dataSource, double value, DateTime? timestamp = null)
    {
        var actualTimestamp = timestamp ?? DateTime.UtcNow;
        var buffer = _buffers.GetOrAdd(
            dataSource,
            _ =>
            {
                _logger.Debug(
                    "Creating new buffer for data source {DataSource} with capacity {Capacity}",
                    dataSource,
                    _defaultCapacity
                );
                return new TimeSeriesBuffer(dataSource, _defaultCapacity, _logger, _metrics);
            }
        );

        _logger.Debug(
            "Adding value {Value} at {Timestamp} to {DataSource}",
            value,
            actualTimestamp,
            dataSource
        );
        buffer.Add(actualTimestamp, value);

        // Update time series metrics
        _metrics.RecordTimeSeriesUpdate(dataSource);
        _metrics.RecordSensorUpdate(dataSource);

        // Update sensor metrics
        _metrics.UpdateSensorValue(dataSource, value);
    }

    /// <summary>
    /// Gets values within a time window for a specific data source
    /// </summary>
    /// <param name="dataSource">The name of the data source</param>
    /// <param name="duration">The duration of the time window</param>
    /// <returns>An array of timestamp-value pairs within the window</returns>
    public IEnumerable<(DateTime Timestamp, double Value)> GetTimeWindow(
        string dataSource,
        TimeSpan duration
    )
    {
        if (!_buffers.TryGetValue(dataSource, out var buffer))
        {
            _logger.Warning(
                "No buffer found for data source {DataSource}. Available sources: {@Sources}",
                dataSource,
                _buffers.Keys
            );
            _metrics.RecordSensorReadError(dataSource, "BufferNotFound");
            return Array.Empty<(DateTime, double)>();
        }

        _logger.Debug(
            "Retrieving time window of {Duration} for {DataSource}",
            duration,
            dataSource
        );
        var values = buffer.GetTimeWindow(duration);
        _logger.Debug("Retrieved {Count} values for {DataSource}", values.Length, dataSource);

        return values;
    }

    /// <summary>
    /// Clears all data for a specific data source
    /// </summary>
    /// <param name="dataSource">The name of the data source to clear</param>
    public void Clear(string dataSource)
    {
        if (_buffers.TryGetValue(dataSource, out var buffer))
        {
            _logger.Information("Clearing buffer for data source {DataSource}", dataSource);
            buffer.Clear();
        }
        else
        {
            _logger.Warning("Attempted to clear non-existent buffer for {DataSource}", dataSource);
            _metrics.RecordSensorReadError(dataSource, "BufferNotFound");
        }
    }

    /// <summary>
    /// Clears all data from all buffers
    /// </summary>
    public void ClearAll()
    {
        _logger.Information(
            "Clearing all time series buffers. Current sources: {@Sources}",
            _buffers.Keys
        );
        foreach (var buffer in _buffers.Values)
        {
            buffer.Clear();
        }
    }

    public Task<double[]> GetHistoricalDataAsync(string sensor, TimeSpan duration)
    {
        if (!_buffers.TryGetValue(sensor, out var buffer))
        {
            _logger.Warning("No buffer found for sensor {Sensor}", sensor);
            return Task.FromResult(Array.Empty<double>());
        }

        return Task.FromResult(buffer.GetValues(duration));
    }

    public Task<bool> CheckThresholdOverTimeAsync(
        string dataSource,
        double threshold,
        TimeSpan duration
    )
    {
        if (!_buffers.TryGetValue(dataSource, out var buffer))
        {
            _logger.Warning("No buffer found for data source {DataSource}", dataSource);
            _metrics.RecordSensorReadError(dataSource, "BufferNotFound");
            return Task.FromResult(false);
        }

        return Task.FromResult(buffer.IsThresholdMaintained(threshold, duration));
    }
}
