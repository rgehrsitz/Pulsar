// File: Pulsar.Compiler/Config/Templates/Runtime/Buffers/RingBufferManager.cs
// Version: 1.0.0

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Beacon.Runtime.Buffers
{
    public class RingBufferManager
    {
        private readonly int _capacity;
        private readonly ConcurrentDictionary<string, Queue<(DateTime Timestamp, double Value)>> _buffers = 
            new ConcurrentDictionary<string, Queue<(DateTime Timestamp, double Value)>>();
        private readonly IDateTimeProvider _dateTimeProvider;

        public RingBufferManager(int capacity, IDateTimeProvider? dateTimeProvider = null)
        {
            _capacity = capacity;
            _dateTimeProvider = dateTimeProvider ?? new SystemDateTimeProvider();
        }

        public void AddValue(string key, double value)
        {
            var buffer = _buffers.GetOrAdd(key, _ => new Queue<(DateTime, double)>(_capacity));
            
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

        public IReadOnlyList<(DateTime Timestamp, double Value)> GetValues(string key)
        {
            if (_buffers.TryGetValue(key, out var buffer))
            {
                lock (buffer)
                {
                    return new List<(DateTime, double)>(buffer);
                }
            }
            
            return Array.Empty<(DateTime, double)>();
        }

        public double? GetLastValue(string key)
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
