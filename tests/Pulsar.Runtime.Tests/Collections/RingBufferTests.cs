using System;
using System.Linq;
using Pulsar.Runtime.Collections;
using Xunit;

namespace Pulsar.Runtime.Tests.Collections;

public class RingBufferTests
{
    [Fact]
    public void Constructor_InvalidCapacity_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new RingBuffer<int>(0));
        Assert.Throws<ArgumentException>(() => new RingBuffer<int>(-1));
    }

    [Fact]
    public void Add_BelowCapacity_AddsItems()
    {
        var buffer = new RingBuffer<int>(3);
        buffer.Add(1);
        buffer.Add(2);

        Assert.Equal(2, buffer.Count);
        Assert.Equal(1, buffer[0]);
        Assert.Equal(2, buffer[1]);
    }

    [Fact]
    public void Add_ExceedsCapacity_OverwritesOldest()
    {
        var buffer = new RingBuffer<int>(3);
        buffer.Add(1);
        buffer.Add(2);
        buffer.Add(3);
        buffer.Add(4);

        Assert.Equal(3, buffer.Count);
        Assert.Equal(2, buffer[0]);
        Assert.Equal(3, buffer[1]);
        Assert.Equal(4, buffer[2]);
    }

    [Fact]
    public void GetWindow_ValidSize_ReturnsCorrectWindow()
    {
        var buffer = new RingBuffer<int>(5);
        for (int i = 1; i <= 5; i++)
            buffer.Add(i);

        var window = buffer.GetWindow(3);
        Assert.Equal(3, window.Length);
        Assert.Equal(new[] { 3, 4, 5 }, window);
    }

    [Fact]
    public void GetWindow_SizeLargerThanCount_ReturnsAllItems()
    {
        var buffer = new RingBuffer<int>(5);
        buffer.Add(1);
        buffer.Add(2);

        var window = buffer.GetWindow(3);
        Assert.Equal(2, window.Length);
        Assert.Equal(new[] { 1, 2 }, window);
    }

    [Fact]
    public void GetTimeWindow_ReturnsItemsInTimeRange()
    {
        var buffer = new RingBuffer<(DateTime Time, int Value)>(5);
        var now = DateTime.UtcNow;

        buffer.Add((now.AddMinutes(-5), 1));
        buffer.Add((now.AddMinutes(-3), 2));
        buffer.Add((now.AddMinutes(-1), 3));

        var window = buffer.GetTimeWindow(TimeSpan.FromMinutes(2), x => x.Time).ToArray();
        Assert.Single(window);
        Assert.Equal(3, window[0].Value);
    }

    [Fact]
    public void Clear_RemovesAllItems()
    {
        var buffer = new RingBuffer<int>(3);
        buffer.Add(1);
        buffer.Add(2);
        buffer.Clear();

        Assert.Equal(0, buffer.Count);
        Assert.Empty(buffer);
    }

    [Fact]
    public void Enumerable_ReturnsItemsInOrder()
    {
        var buffer = new RingBuffer<int>(3);
        buffer.Add(1);
        buffer.Add(2);
        buffer.Add(3);
        buffer.Add(4);

        Assert.Equal(new[] { 2, 3, 4 }, buffer.ToArray());
    }

    [Fact]
    public void GetAll_ReturnsItemsInChronologicalOrder()
    {
        // Arrange
        var buffer = new RingBuffer<(DateTime Time, int Value)>(5);
        var now = DateTime.UtcNow;
        buffer.Add((now.AddSeconds(-4), 1));
        buffer.Add((now.AddSeconds(-3), 2));
        buffer.Add((now.AddSeconds(-2), 3));
        buffer.Add((now.AddSeconds(-1), 4));
        buffer.Add((now, 5));

        // Act
        var items = buffer.GetWindow(5);

        // Assert
        Assert.Equal(5, items.Length);
        Assert.Equal(1, items[0].Value);
        Assert.Equal(2, items[1].Value);
        Assert.Equal(3, items[2].Value);
        Assert.Equal(4, items[3].Value);
        Assert.Equal(5, items[4].Value);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void Indexer_InvalidIndex_ThrowsArgumentOutOfRangeException(int index)
    {
        var buffer = new RingBuffer<int>(3);
        buffer.Add(1);
        buffer.Add(2);

        Assert.Throws<ArgumentOutOfRangeException>(() => buffer[index]);
    }
}
