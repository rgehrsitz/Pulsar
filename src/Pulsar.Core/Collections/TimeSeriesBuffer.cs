using System;
using System.Collections.Generic;
using Pulsar.Core.Services;
using Serilog;

namespace Pulsar.Core.Collections;

public class TimeSeriesBuffer
{
    private readonly double[] _values;
    private readonly DateTime[] _timestamps;
    private int _head;
    private readonly int _capacity;
    private int _count;
    private readonly string _sensorName;
    private readonly ILogger _logger;
    private readonly IMetricsService _metrics;

    public TimeSeriesBuffer(
        string sensorName,
        int capacity,
        ILogger logger,
        IMetricsService metrics
    )
    {
        _sensorName = sensorName;
        _capacity = capacity;
        _values = new double[capacity];
        _timestamps = new DateTime[capacity];
        _head = 0;
        _count = 0;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
    }

    public void Add(DateTime timestamp, double value)
    {
        _values[_head] = value;
        _timestamps[_head] = timestamp;
        _head = (_head + 1) % _capacity;
        _count = Math.Min(_count + 1, _capacity);  // Fix capitalization of Min
        _metrics.RecordTimeSeriesBufferSize(_sensorName, _count);
    }

    public void Clear()
    {
        _head = 0;
        _count = 0;
        _metrics.RecordTimeSeriesBufferSize(_sensorName, 0);
    }

    public bool IsThresholdMaintained(double threshold, TimeSpan duration)
    {
        if (_count == 0)
            return false;

        var cutoff = DateTime.UtcNow - duration;
        int idx = _head;
        bool hasValidData = false;

        for (int i = 0; i < _count; i++)
        {
            idx = (idx - 1 + _capacity) % _capacity;
            if (_timestamps[idx] < cutoff)
                break;

            hasValidData = true;
            if (_values[idx] <= threshold)
                return false;
        }

        return hasValidData;
    }

    public (DateTime Timestamp, double Value)[] GetTimeWindow(TimeSpan duration)
    {
        var cutoff = DateTime.UtcNow - duration;
        var list = new List<(DateTime, double)>();
        int idx = _head;

        for (int i = 0; i < _count; i++)
        {
            idx = (idx - 1 + _capacity) % _capacity;
            if (_timestamps[idx] < cutoff)
                break;

            list.Add((_timestamps[idx], _values[idx]));
        }

        list.Reverse();
        return list.ToArray();
    }

    public double[] GetValues(TimeSpan duration)
    {
        var values = GetTimeWindow(duration);
        return Array.ConvertAll(values, v => v.Value);
    }

    // ...existing code...
}
