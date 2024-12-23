using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Pulsar.Runtime.Services;

public interface ISensorTemporalBufferService
{
    Task<IEnumerable<(DateTime Timestamp, double Value)>> GetSensorHistory(
        string sensorId,
        TimeSpan maxDuration
    );
    Task AddSensorValue(string sensorId, double value);
}
