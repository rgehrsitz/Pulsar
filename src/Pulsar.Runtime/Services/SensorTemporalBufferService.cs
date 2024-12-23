using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Serilog;

namespace Pulsar.Runtime.Services
{
    /// <summary>
    /// Service that maintains temporal buffers for sensors that need short-term historical data
    /// </summary>
    public class SensorTemporalBufferService : ISensorTemporalBufferService
    {
        private readonly ConcurrentDictionary<string, TimeSeriesBuffer> _buffers;
        private readonly ILogger _logger;
        private readonly TimeSpan _maxDuration;

        public SensorTemporalBufferService(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _buffers = new ConcurrentDictionary<string, TimeSeriesBuffer>();
            _maxDuration = TimeSpan.FromHours(1);
        }

        public Task<IEnumerable<(DateTime Timestamp, double Value)>> GetSensorHistory(
            string sensorId,
            TimeSpan maxDuration
        )
        {
            if (!_buffers.TryGetValue(sensorId, out var buffer))
            {
                return Task.FromResult<IEnumerable<(DateTime, double)>>(Array.Empty<(DateTime, double)>());
            }

            var cutoff = DateTime.UtcNow - maxDuration;
            var results = buffer.GetValues().Where(x => x.Timestamp >= cutoff).ToList();
            return Task.FromResult<IEnumerable<(DateTime, double)>>(results);
        }

        public Task AddSensorValue(string sensorId, double value)
        {
            UpdateSensor(sensorId, value);
            return Task.CompletedTask;
        }

        public async Task HandleSensorDataAsync(CancellationToken cancellationToken)
        {
            // ...existing code...
            await Task.Delay(1, cancellationToken);
            // ...existing code...
            return;
        }

        private void UpdateSensor(string sensorName, double value)
        {
            var buffer = _buffers.GetOrAdd(sensorName, _ => new TimeSeriesBuffer(_maxDuration));
            buffer.Add(value);
        }

        private class TimeSeriesBuffer
        {
            private readonly List<(DateTime Timestamp, double Value)> _values;
            private readonly TimeSpan _maxDuration;
            private readonly object _lock = new();

            public TimeSeriesBuffer(TimeSpan maxDuration)
            {
                _values = new List<(DateTime, double)>();
                _maxDuration = maxDuration;
            }

            public void Add(double value)
            {
                lock (_lock)
                {
                    var now = DateTime.UtcNow;
                    _values.Add((now, value));

                    // Remove old values
                    var cutoff = now - _maxDuration;
                    _values.RemoveAll(x => x.Timestamp < cutoff);
                }
            }

            public IEnumerable<(DateTime Timestamp, double Value)> GetValues()
            {
                lock (_lock)
                {
                    return _values.ToList();
                }
            }
        }
    }
}
