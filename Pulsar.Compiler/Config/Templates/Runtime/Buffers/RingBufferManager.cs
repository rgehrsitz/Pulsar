// File: Pulsar.Compiler/Config/Templates/Runtime/Buffers/RingBufferManager.cs
// Version: 1.0.0

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Beacon.Runtime.Buffers
{
    public class RingBufferManager
    {
        private readonly int _capacity;
        private readonly ConcurrentDictionary<
            string,
            Queue<(DateTime Timestamp, object Value)>
        > _buffers = new ConcurrentDictionary<string, Queue<(DateTime Timestamp, object Value)>>();
        private readonly IDateTimeProvider _dateTimeProvider;

        public RingBufferManager(int capacity, IDateTimeProvider? dateTimeProvider = null)
        {
            _capacity = capacity;
            _dateTimeProvider = dateTimeProvider ?? new SystemDateTimeProvider();
        }

        public void AddValue(string key, object value)
        {
            var buffer = _buffers.GetOrAdd(key, _ => new Queue<(DateTime, object)>(_capacity));

            lock (buffer)
            {
                // Add new value
                buffer.Enqueue((_dateTimeProvider.UtcNow, value));

                // Remove oldest value if over capacity
                while (buffer.Count > _capacity)
                {
                    buffer.Dequeue();
                }
            }
        }

        public IReadOnlyList<(DateTime Timestamp, object Value)> GetValues(string key)
        {
            if (_buffers.TryGetValue(key, out var buffer))
            {
                lock (buffer)
                {
                    return new List<(DateTime, object)>(buffer);
                }
            }

            return Array.Empty<(DateTime, object)>();
        }

        public IReadOnlyList<(DateTime Timestamp, object Value)> GetValues(
            string key,
            TimeSpan duration
        )
        {
            if (_buffers.TryGetValue(key, out var buffer))
            {
                lock (buffer)
                {
                    var cutoffTime = _dateTimeProvider.UtcNow - duration;
                    return new List<(DateTime, object)>(
                        buffer.Where(v => v.Timestamp >= cutoffTime)
                    );
                }
            }

            return Array.Empty<(DateTime, object)>();
        }

        public object? GetLastValue(string key)
        {
            if (_buffers.TryGetValue(key, out var buffer) && buffer.Count > 0)
            {
                lock (buffer)
                {
                    if (buffer.Count > 0)
                    {
                        return buffer.ToArray()[buffer.Count - 1].Value;
                    }
                }
            }

            return null;
        }

        public void Clear(string key)
        {
            if (_buffers.TryGetValue(key, out var buffer))
            {
                lock (buffer)
                {
                    buffer.Clear();
                }
            }
        }

        public void ClearAll()
        {
            foreach (var key in _buffers.Keys)
            {
                Clear(key);
            }
        }
    }
}
