using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pulsar.Runtime.Collections;
using Serilog;

namespace Pulsar.Runtime.Services
{
    /// <summary>
    /// Service that maintains temporal buffers for sensors that need short-term historical data
    /// </summary>
    public class SensorTemporalBufferService
    {
        private readonly ConcurrentDictionary<string, TimeSeriesBuffer> _buffers;
        private readonly ILogger _logger;
        private readonly IMetricsService _metrics;
        private readonly int _defaultBufferCapacity;
        private readonly TimeSpan _maxBufferDuration;

        public SensorTemporalBufferService(
            ILogger logger,
            IMetricsService metrics,
            int defaultBufferCapacity = 60, // 60 samples by default
            TimeSpan? maxBufferDuration = null // Default 5 seconds
        )
        {
            if (logger == null) throw new ArgumentNullException(nameof(logger));
            if (metrics == null) throw new ArgumentNullException(nameof(metrics));
            if (defaultBufferCapacity <= 0) throw new ArgumentException("Buffer capacity must be positive", nameof(defaultBufferCapacity));

            _buffers = new ConcurrentDictionary<string, TimeSeriesBuffer>();
            _logger = logger.ForContext<SensorTemporalBufferService>();
            _metrics = metrics;
            _defaultBufferCapacity = defaultBufferCapacity;
            _maxBufferDuration = maxBufferDuration ?? TimeSpan.FromSeconds(5);
        }

        /// <summary>
        /// Updates the value for a sensor, creating a buffer if one doesn't exist
        /// </summary>
        public void UpdateSensor(string sensorName, double value, DateTime? timestamp = null)
        {
            var buffer = _buffers.GetOrAdd(
                sensorName,
                name => new TimeSeriesBuffer(name, _defaultBufferCapacity, _logger, _metrics)
            );

            buffer.Add(timestamp ?? DateTime.UtcNow, value);
        }

        /// <summary>
        /// Gets historical values for a sensor within the specified duration
        /// </summary>
        /// <returns>Empty array if no buffer exists for the sensor</returns>
        public (DateTime Timestamp, double Value)[] GetSensorHistory(string sensorName, TimeSpan duration)
        {
            if (duration > _maxBufferDuration)
            {
                _logger.Warning(
                    "Requested duration {Duration} exceeds max buffer duration {MaxDuration}",
                    duration,
                    _maxBufferDuration
                );
                duration = _maxBufferDuration;
            }

            if (_buffers.TryGetValue(sensorName, out var buffer))
            {
                return buffer.GetTimeWindow(duration);
            }

            return Array.Empty<(DateTime, double)>();
        }

        /// <summary>
        /// Removes the temporal buffer for a sensor
        /// </summary>
        public void RemoveSensor(string sensorName)
        {
            if (_buffers.TryRemove(sensorName, out var buffer))
            {
                buffer.Clear();
            }
        }

        /// <summary>
        /// Gets whether a sensor has a temporal buffer
        /// </summary>
        public bool HasTemporalBuffer(string sensorName)
        {
            return _buffers.ContainsKey(sensorName);
        }
    }
}
