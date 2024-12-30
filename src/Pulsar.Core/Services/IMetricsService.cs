namespace Pulsar.Core.Services;

public interface IMetricsService
{
    void UpdateSensorValue(string sensor, double value);
    void RecordSensorUpdate(string sensor);
    void RecordTimeSeriesUpdate(string sensor);
    void RecordSensorReadError(string sensor, string errorType);
    void RecordTimeSeriesBufferSize(string sensor, int size);
    void RecordTimeSeriesOverflow(string sensor);
    void RecordThresholdEvaluation(string sensor, bool result, int durationMs);
}
