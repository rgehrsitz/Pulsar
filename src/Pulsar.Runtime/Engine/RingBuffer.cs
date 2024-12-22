namespace Pulsar.Runtime.Engine;

/// <summary>
/// A fixed-size ring buffer for storing historical values
/// </summary>
public class RingBuffer<T>
{
    private readonly T[] _buffer;
    private int _currentIndex;
    private readonly object _lock = new();

    public RingBuffer(int size)
    {
        _buffer = new T[size];
        _currentIndex = 0;
    }

    public void Add(T item)
    {
        if (item == null)
        {
            throw new ArgumentNullException(
                nameof(item),
                "Cannot add null values to the ring buffer."
            );
        }

        lock (_lock)
        {
            _buffer[_currentIndex] = item;
            _currentIndex = (_currentIndex + 1) % _buffer.Length;
        }
    }

    public IEnumerable<T> GetValues()
    {
        lock (_lock)
        {
            var values = new T[_buffer.Length];
            Array.Copy(_buffer, values, _buffer.Length);
            return values;
        }
    }
}
