using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using NRedisStack;
using NRedisStack.RedisStackCommands;
using StackExchange.Redis;
using Xunit;
using Pulsar.Runtime.Configuration;
using Pulsar.Runtime.Storage;
using Serilog;
using NRedisStack.DataTypes;

namespace Pulsar.Runtime.Tests.Storage
{
    public class RedisSensorDataProviderTests
    {
        private readonly Mock<IConnectionMultiplexer> _multiplexerMock;
        private readonly Mock<IDatabase> _databaseMock;
        private readonly Mock<ILogger> _loggerMock;
        private readonly Mock<ITimeSeriesCommands> _tsCommandsMock;
        private readonly RedisClusterConfiguration _clusterConfig;
        private readonly RedisSensorDataProvider _provider;
        private readonly string _keyPrefix = "sensor:";
        private readonly string _tsPrefix = "ts:";

        public RedisSensorDataProviderTests()
        {
            _multiplexerMock = new Mock<IConnectionMultiplexer>();
            _databaseMock = new Mock<IDatabase>();
            _loggerMock = new Mock<ILogger>();
            _tsCommandsMock = new Mock<ITimeSeriesCommands>();
            _clusterConfig = new RedisClusterConfiguration(_loggerMock.Object, "master", new[] { "localhost:26379" }, Environment.MachineName);

            _multiplexerMock.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(_databaseMock.Object);

            // Setup logger context
            _loggerMock.Setup(x => x.ForContext<RedisSensorDataProvider>())
                .Returns(_loggerMock.Object);

            // Setup Redis connection
            _databaseMock.Setup(d => d.PingAsync(It.IsAny<CommandFlags>()))
                .ReturnsAsync(TimeSpan.Zero);

            // Setup time series commands
            _databaseMock.Setup(d => d.TS())
                .Returns(_tsCommandsMock.Object);

            _provider = new RedisSensorDataProvider(_clusterConfig, _loggerMock.Object, _keyPrefix, _tsPrefix);
        }

        [Fact]
        public async Task GetCurrentDataAsync_ReturnsData()
        {
            // Arrange
            var keys = new RedisKey[] { "sensor:temp1", "sensor:temp2" };
            var values = new RedisValue[] { "25.0", "30.0" };

            _databaseMock.Setup(d => d.StringGetAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(values);

            // Act
            var result = await _provider.GetCurrentDataAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Equal(25.0, result["temp1"]);
            Assert.Equal(30.0, result["temp2"]);
        }

        [Fact]
        public async Task GetHistoricalDataAsync_ReturnsHistory()
        {
            // Arrange
            var sensorId = "temp1";
            var now = DateTimeOffset.UtcNow;
            var duration = TimeSpan.FromHours(1);
            var expectedData = new[]
            {
                new TimeSeriesTuple(now.AddMinutes(-30).ToUnixTimeMilliseconds(), 25.0),
                new TimeSeriesTuple(now.ToUnixTimeMilliseconds(), 26.0)
            };

            _tsCommandsMock.Setup(ts => ts.RangeAsync(
                $"{_tsPrefix}{sensorId}",
                It.IsAny<long>(),
                It.IsAny<long>(),
                It.IsAny<bool>()))
                .ReturnsAsync(expectedData);

            // Act
            var result = await _provider.GetHistoricalDataAsync(sensorId, duration);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Equal(25.0, result[0].Value);
            Assert.Equal(26.0, result[1].Value);
        }

        [Fact]
        public async Task SetSensorDataAsync_StoresData()
        {
            // Arrange
            var values = new Dictionary<string, object>
            {
                { "temp1", 25.0 },
                { "temp2", 30.0 }
            };

            _databaseMock.Setup(d => d.CreateBatch())
                .Returns(Mock.Of<IBatch>());

            _tsCommandsMock.Setup(ts => ts.CreateAsync(
                It.IsAny<string>(),
                It.IsAny<CreateOptions>()))
                .ReturnsAsync(true);

            _tsCommandsMock.Setup(ts => ts.AddAsync(
                It.IsAny<string>(),
                It.IsAny<long>(),
                It.IsAny<double>()))
                .ReturnsAsync(It.IsAny<long>());

            // Act
            await _provider.SetSensorDataAsync(values);

            // Assert
            _tsCommandsMock.Verify(ts => ts.CreateAsync(
                It.Is<string>(key => key.StartsWith(_tsPrefix)),
                It.IsAny<CreateOptions>()),
                Times.Exactly(2));

            _tsCommandsMock.Verify(ts => ts.AddAsync(
                It.Is<string>(key => key.StartsWith(_tsPrefix)),
                It.IsAny<long>(),
                It.IsAny<double>()),
                Times.Exactly(2));
        }

        [Fact]
        public async Task GetCurrentDataAsync_ConnectionFailure_ThrowsException()
        {
            // Arrange
            _databaseMock.Setup(d => d.StringGetAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()))
                .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection failed"));

            // Act & Assert
            await Assert.ThrowsAsync<RedisConnectionException>(() =>
                _provider.GetCurrentDataAsync());
        }

        [Fact]
        public async Task GetHistoricalDataAsync_ConnectionFailure_ThrowsException()
        {
            // Arrange
            _tsCommandsMock.Setup(ts => ts.RangeAsync(
                It.IsAny<string>(),
                It.IsAny<long>(),
                It.IsAny<long>(),
                It.IsAny<bool>()))
                .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection failed"));

            // Act & Assert
            await Assert.ThrowsAsync<RedisConnectionException>(() =>
                _provider.GetHistoricalDataAsync("temp1", TimeSpan.FromHours(1)));
        }
    }
}
