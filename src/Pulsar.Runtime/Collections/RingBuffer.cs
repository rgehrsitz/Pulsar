using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Pulsar.Runtime.Collections;

/// <summary>
/// A fixed-size circular buffer that maintains a sliding window of values
/// </summary>
/// <typeparam name="T">The type of elements in the buffer</typeparam>
public class RingBuffer<T> : IEnumerable<T>
{
    private readonly T[] _buffer;
    private int _start;
    private int _count;

    /// <summary>
    /// Gets the capacity of the buffer
    /// </summary>
    public int Capacity => _buffer.Length;

    /// <summary>
    /// Gets the current number of elements in the buffer
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Gets whether the buffer is full
    /// </summary>
    public bool IsFull => _count == Capacity;

    /// <summary>
    /// Creates a new ring buffer with the specified capacity
    /// </summary>
    /// <param name="capacity">The maximum number of elements the buffer can hold</param>
    public RingBuffer(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentException("Capacity must be greater than 0", nameof(capacity));

        _buffer = new T[capacity];
        _start = 0;
        _count = 0;
    }

    /// <summary>
    /// Adds an item to the buffer, overwriting the oldest item if the buffer is full
    /// </summary>
    /// <param name="item">The item to add</param>
    public void Add(T item)
    {
        var index = (_start + _count) % Capacity;
        _buffer[index] = item;

        if (_count < Capacity)
            _count++;
        else
            _start = (_start + 1) % Capacity;
    }

    /// <summary>
    /// Gets the value at the specified index in the buffer
    /// </summary>
    /// <param name="index">The index of the value to get</param>
    /// <returns>The value at the specified index</returns>
    public T this[int index]
    {
        get
        {
            if (index < 0 || index >= _count)
                throw new ArgumentOutOfRangeException(nameof(index));

            return _buffer[(_start + index) % Capacity];
        }
    }

    /// <summary>
    /// Gets a window of the most recent values in the buffer
    /// </summary>
    /// <param name="windowSize">The size of the window</param>
    /// <returns>An array containing the most recent values</returns>
    public T[] GetWindow(int windowSize)
    {
        if (windowSize <= 0)
            throw new ArgumentException("Window size must be greater than 0", nameof(windowSize));

        if (windowSize > _count)
            windowSize = _count;

        var window = new T[windowSize];
        var startIndex = _count - windowSize;

        for (int i = 0; i < windowSize; i++)
        {
            window[i] = this[startIndex + i];
        }

        return window;
    }

    /// <summary>
    /// Gets a time window of values based on their timestamps
    /// </summary>
    /// <param name="duration">The duration of the window in milliseconds</param>
    /// <param name="getTimestamp">Function to extract timestamp from value</param>
    /// <returns>An enumerable of values within the specified time window</returns>
    public IEnumerable<T> GetTimeWindow(TimeSpan duration, Func<T, DateTime> getTimestamp)
    {
        if (duration <= TimeSpan.Zero)
            throw new ArgumentException("Duration must be greater than 0", nameof(duration));

        if (getTimestamp == null)
            throw new ArgumentNullException(nameof(getTimestamp));

        var cutoff = DateTime.UtcNow - duration;
        return this.Where(x => getTimestamp(x) >= cutoff);
    }

    /// <summary>
    /// Clears all items from the buffer
    /// </summary>
    public void Clear()
    {
        Array.Clear(_buffer, 0, _buffer.Length);
        _start = 0;
        _count = 0;
    }

    /// <summary>
    /// Converts the buffer to an array
    /// </summary>
    /// <returns>An array containing all elements in the buffer</returns>
    public T[] ToArray()
    {
        var array = new T[_count];
        for (int i = 0; i < _count; i++)
        {
            var index = (_start + i) % Capacity;
            array[i] = _buffer[index];
        }
        return array;
    }

    public IEnumerator<T> GetEnumerator()
    {
        for (int i = 0; i < _count; i++)
        {
            var index = (_start + i) % Capacity;
            yield return _buffer[index];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
