// File: Pulsar.Compiler/Config/Templates/Runtime/Services/RedisMetrics.cs
// Version: 1.0.0

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

namespace Beacon.Runtime.Services
{
    public class RedisMetrics
    {
        private readonly ConcurrentDictionary<string, long> _errorCounts =
            new ConcurrentDictionary<string, long>();
        private readonly ConcurrentDictionary<string, long> _operationCounts =
            new ConcurrentDictionary<string, long>();
        private readonly ConcurrentDictionary<string, Stopwatch> _operationTimers =
            new ConcurrentDictionary<string, Stopwatch>();
        private int _connectionCount = 0;
        private int _retryCount = 0;

        public void TrackError(string errorType)
        {
            _errorCounts.AddOrUpdate(errorType, 1, (_, count) => count + 1);
        }

        public void RecordError(string errorType)
        {
            TrackError(errorType);
        }

        public void RecordOperation(string operationName, TimeSpan duration)
        {
            _operationCounts.AddOrUpdate(operationName, 1, (_, count) => count + 1);
        }

        public void IncrementRetryCount()
        {
            Interlocked.Increment(ref _retryCount);
        }

        public IDisposable TrackOperation(string operationName)
        {
            _operationCounts.AddOrUpdate(operationName, 1, (_, count) => count + 1);
            var timer = new Stopwatch();
            timer.Start();
            _operationTimers[operationName] = timer;

            return new OperationTracker(this, operationName);
        }

        private void EndOperation(string operationName)
        {
            if (_operationTimers.TryRemove(operationName, out var timer))
            {
                timer.Stop();
            }
        }

        public void UpdateConnectionCount(int delta)
        {
            Interlocked.Add(ref _connectionCount, delta);
        }

        private class OperationTracker : IDisposable
        {
            private readonly RedisMetrics _metrics;
            private readonly string _operationName;

            public OperationTracker(RedisMetrics metrics, string operationName)
            {
                _metrics = metrics;
                _operationName = operationName;
            }

            public void Dispose()
            {
                _metrics.EndOperation(_operationName);
            }
        }
    }
}
