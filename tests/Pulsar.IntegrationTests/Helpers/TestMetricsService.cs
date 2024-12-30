using System;
using System.Collections.Generic;
using Pulsar.Core.Services;

namespace Pulsar.IntegrationTests.Helpers;

/// <summary>
/// A test implementation of IMetricsService that tracks metric updates for testing
/// </summary>
public class TestMetricsService : IMetricsService
{
    private readonly Dictionary<string, int> _updateCounts = new();

    public IReadOnlyDictionary<string, int> UpdateCounts => _updateCounts;

    public void UpdateSensorValue(string sensor, double value) { }
    public void RecordSensorUpdate(string sensor) 
    {
        _updateCounts[sensor] = _updateCounts.GetValueOrDefault(sensor) + 1;
    }
    public void RecordTimeSeriesUpdate(string sensor) { }
    public void RecordSensorReadError(string sensor, string errorType) { }
    public void RecordTimeSeriesBufferSize(string sensor, int size) { }
    public void RecordTimeSeriesOverflow(string sensor) { }
    public void RecordThresholdEvaluation(string sensor, bool result, int durationMs) { }

    public void ResetCounts()
    {
        foreach (var sensor in _updateCounts.Keys.ToList())
        {
            _updateCounts[sensor] = 0;
        }
    }
}
