using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Moq;
using NRedisStack;
using NRedisStack.DataTypes;
using NRedisStack.RedisStackCommands;
using StackExchange.Redis;
using Xunit;
using Serilog;
using System.Threading;
using System.Runtime.CompilerServices;
using Pulsar.Runtime.Storage;
using Pulsar.Runtime.Configuration;

namespace Pulsar.Runtime.Tests.Storage;

public class RedisSensorDataProviderTests
{
    private readonly Mock<ILogger> _loggerMock;
    private readonly Mock<IConnectionMultiplexer> _multiplexerMock;
    private readonly Mock<IDatabase> _databaseMock;
    private readonly Mock<TimeSeriesCommands> _tsCommandsMock;
    private readonly RedisClusterConfiguration _clusterConfig;
    private readonly Mock<IServer> _serverMock;
    private readonly string _keyPrefix = "sensor:";
    private readonly string _tsPrefix = "ts:";
    
    public RedisSensorDataProviderTests()
    {
        _loggerMock = new Mock<ILogger>();
        _multiplexerMock = new Mock<IConnectionMultiplexer>();
        _databaseMock = new Mock<IDatabase>();
        _tsCommandsMock = new Mock<TimeSeriesCommands>();
        _serverMock = new Mock<IServer>();
        _clusterConfig = new RedisClusterConfiguration(_loggerMock.Object, "master", new[] { "localhost:26379" }, Environment.MachineName);
        
        var endPoint = new DnsEndPoint("localhost", 26379);
        _multiplexerMock.Setup(m => m.GetServer(endPoint))
            .Returns(_serverMock.Object);
        
        _multiplexerMock.Setup(m => m.GetDatabase(0, null))
            .Returns(_databaseMock.Object);
        
        _databaseMock.Setup(m => m.TS())
            .Returns(_tsCommandsMock.Object);

        _databaseMock.Setup(m => m.Ping(CommandFlags.None))
            .Returns(TimeSpan.Zero);

        _multiplexerMock.Setup(m => m.IsConnected)
            .Returns(true);

        _loggerMock.Setup(m => m.ForContext<RedisSensorDataProvider>())
            .Returns(_loggerMock.Object);

        _clusterConfig.GetType()
            .GetMethod("GetConnection", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.Invoke(_clusterConfig, Array.Empty<object>());
    }

    [Fact]
    public async Task GetCurrentDataAsync_ShouldReturnEmptyDictionary_WhenNoKeysExist()
    {
        // Arrange
        _serverMock.Setup(s => s.Keys(pattern: "sensor:*", database: 0))
            .Returns(Array.Empty<RedisKey>().ToAsyncEnumerable());

        var provider = new RedisSensorDataProvider(_clusterConfig, _loggerMock.Object);

        // Act
        var result = await provider.GetCurrentDataAsync();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetCurrentDataAsync_ShouldReturnSensorValues_WhenKeysExist()
    {
        // Arrange
        var keys = new[] { "sensor:temp1", "sensor:temp2" }.Select(k => (RedisKey)k);
        var values = new[] { "25.5", "30.0" }.Select(v => (RedisValue)v);

        _serverMock.Setup(s => s.Keys(pattern: "sensor:*", database: 0))
            .Returns(keys.ToAsyncEnumerable());

        _databaseMock.Setup(d => d.StringGet(It.IsAny<RedisKey[]>(), CommandFlags.None))
            .Returns(values.ToArray());

        var provider = new RedisSensorDataProvider(_clusterConfig, _loggerMock.Object);

        // Act
        var result = await provider.GetCurrentDataAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(25.5, result["temp1"]);
        Assert.Equal(30.0, result["temp2"]);
    }

    [Fact]
    public async Task SetSensorDataAsync_ShouldCreateTimeSeriesAndSetValues()
    {
        // Arrange
        var values = new Dictionary<string, object>
        {
            { "temp1", 25.5 },
            { "temp2", 30.0 }
        };

        _tsCommandsMock.Setup(ts => ts.Create(It.IsAny<string>()))
            .Returns(true);

        _tsCommandsMock.Setup(ts => ts.Add(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<double>()))
            .Returns(true);

        var provider = new RedisSensorDataProvider(_clusterConfig, _loggerMock.Object);

        // Act
        await provider.SetSensorDataAsync(values);

        // Verify
        _tsCommandsMock.Verify(ts => ts.Create("ts:temp1"), Times.Once);
        _tsCommandsMock.Verify(ts => ts.Create("ts:temp2"), Times.Once);
        _tsCommandsMock.Verify(ts => ts.Add(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<double>()), Times.Exactly(2));
        _databaseMock.Verify(d => d.StringSet(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), null, CommandFlags.None), Times.Exactly(2));
    }

    [Fact]
    public async Task GetHistoricalDataAsync_ShouldReturnTimeSeriesData()
    {
        // Arrange
        var duration = TimeSpan.FromHours(1);
        var now = DateTimeOffset.UtcNow;
        var timeSeriesData = new[]
        {
            new TimeSeriesEntry(now.ToUnixTimeMilliseconds(), 25.5),
            new TimeSeriesEntry(now.AddMinutes(30).ToUnixTimeMilliseconds(), 26.0)
        };

        _tsCommandsMock.Setup(ts => ts.Range(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<long>()))
            .Returns(timeSeriesData);

        var provider = new RedisSensorDataProvider(_clusterConfig, _loggerMock.Object);

        // Act
        var result = await provider.GetHistoricalDataAsync("temp1", duration);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(25.5, result[0].Value);
        Assert.Equal(26.0, result[1].Value);
    }
}
