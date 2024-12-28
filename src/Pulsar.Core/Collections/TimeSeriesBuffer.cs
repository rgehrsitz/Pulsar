using System;
using System.Collections.Generic;
using Serilog;

namespace Pulsar.Core.Collections;

public class TimeSeriesBuffer
{
    // ...existing code...

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

    public double[] GetValues(TimeSpan duration)
    {
        var values = GetTimeWindow(duration);
        return Array.ConvertAll(values, v => v.Value);
    }

    // ...existing code...
}
