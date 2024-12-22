using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using NRedisStack;
using NRedisStack.RedisStackCommands;
using StackExchange.Redis;
using Xunit;
using System.Linq;
using Pulsar.Runtime.Storage;
using Pulsar.Runtime.Configuration;
using NRedisStack.DataTypes;
using NRedisStack.Literals.Enums;
using Serilog;

namespace Pulsar.Runtime.Tests.Storage
{
    public class RedisSensorDataProviderTests
    {
        private readonly Mock<IConnectionMultiplexer> _multiplexerMock;
        private readonly Mock<IDatabase> _databaseMock;
        private readonly Mock<TimeSeriesCommands> _tsCommandsMock;
        private readonly Mock<Serilog.ILogger> _loggerMock;
        private readonly RedisSensorDataProvider _provider;
        private const string _keyPrefix = "sensor:";
        private const string _tsPrefix = "ts:";
        private readonly RedisClusterConfiguration _clusterConfig;

        public RedisSensorDataProviderTests()
        {
            _multiplexerMock = new Mock<IConnectionMultiplexer>();
            _databaseMock = new Mock<IDatabase>();
            _tsCommandsMock = new Mock<TimeSeriesCommands>();
            _loggerMock = new Mock<Serilog.ILogger>();

            _multiplexerMock.Setup(m => m.GetDatabaseAsync(It.IsAny<int>(), It.IsAny<object>()))
                .ReturnsAsync(_databaseMock.Object);

            _databaseMock.Setup(d => d.TSAsync())
                .ReturnsAsync(_tsCommandsMock.Object);

            _databaseMock.Setup(d => d.KeyTimeToLiveAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(TimeSpan.Zero);

            // Setup time series operations
            var emptyList = new List<TimeSeriesTuple>() as IReadOnlyList<TimeSeriesTuple>;
            _tsCommandsMock.Setup(ts => ts.RangeAsync(
                It.IsAny<string>(),
                It.IsAny<long>(),
                It.IsAny<long>()))
                .ReturnsAsync(emptyList);

            _tsCommandsMock.Setup(ts => ts.CreateAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            _tsCommandsMock.Setup(ts => ts.AddAsync(
                It.IsAny<string>(),
                It.IsAny<long>(),
                It.IsAny<double>()))
                .ReturnsAsync(new TimeStamp(1));

            // Setup specific test data
            var now = DateTimeOffset.UtcNow;
            var duration = TimeSpan.FromHours(1);
            var sensorId = "temp1";
            var expectedData = new[]
            {
                new TimeSeriesTuple(now.AddMinutes(-30).ToUnixTimeMilliseconds(), 25.0),
                new TimeSeriesTuple(now.ToUnixTimeMilliseconds(), 26.0)
            };

            _tsCommandsMock.Setup(ts => ts.RangeAsync(
                $"{_tsPrefix}{sensorId}",
                It.IsAny<long>(),
                It.IsAny<long>()))
                .ReturnsAsync(expectedData);

            _clusterConfig = new RedisClusterConfiguration(
                _loggerMock.Object,
                "master",
                new[] { "localhost:6379" },
                Environment.MachineName);

            _provider = new RedisSensorDataProvider(_clusterConfig, _loggerMock.Object, _keyPrefix, _tsPrefix);
        }

        [Fact]
        public async Task GetCurrentDataAsync_ReturnsData()
        {
            // Arrange
            var keys = new RedisKey[] { "sensor:temp1", "sensor:temp2" };
            var values = new RedisValue[] { "25.0", "30.0" };

            var server = new Mock<IServer>();
            var asyncEnumerable = keys.ToAsyncEnumerable();
            server.Setup(s => s.KeysAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CommandFlags>()))
                .Returns(asyncEnumerable);

            _multiplexerMock.Setup(m => m.GetServerAsync(It.IsAny<string>(), It.IsAny<object>()))
                .ReturnsAsync(server.Object);

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

            var batchMock = new Mock<IBatch>();
            _databaseMock.Setup(d => d.CreateBatch())
                .Returns(batchMock.Object);

            batchMock.Setup(b => b.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), null, When.Always, CommandFlags.None))
                .Returns(Task.FromResult(true));

            // Act
            await _provider.SetSensorDataAsync(values);

            // Assert
            _tsCommandsMock.Verify(ts => ts.CreateAsync(
                It.Is<string>(key => key.StartsWith(_tsPrefix))),
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
                It.IsAny<TsRangeParams>()))
                .Throws(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection failed"));

            var now = DateTimeOffset.UtcNow;
            var duration = TimeSpan.FromHours(1);
            var sensorId = "temp1";

            // Act & Assert
            await Assert.ThrowsAsync<RedisConnectionException>(() =>
                _provider.GetHistoricalDataAsync(sensorId, duration));
        }

        [Fact]
        public async Task GetHistoricalDataAsync_NoData_ReturnsEmptyList()
        {
            // Arrange
            var now = DateTimeOffset.UtcNow;
            var duration = TimeSpan.FromHours(1);
            var sensorId = "temp1";
            var emptyList = new List<TimeSeriesTuple>() as IReadOnlyList<TimeSeriesTuple>;

            _tsCommandsMock.Setup(ts => ts.RangeAsync(
                It.IsAny<string>(),
                It.IsAny<long>(),
                It.IsAny<long>(),
                It.IsAny<TsRangeParams>()))
                .ReturnsAsync(emptyList);

            // Act
            var result = await _provider.GetHistoricalDataAsync(sensorId, duration);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task SetSensorDataAsync_ValidData_Success()
        {
            // Arrange
            var now = DateTimeOffset.UtcNow;
            var values = new Dictionary<string, object>
            {
                { "temp1", 25.0 },
                { "temp2", 26.0 }
            };

            // Act
            await _provider.SetSensorDataAsync(values);

            // Assert
            var createParams = new TsCreateParamsBuilder().Build();
            _tsCommandsMock.Verify(ts => ts.Create(
                It.Is<string>(key => key.StartsWith(_tsPrefix)),
                It.IsAny<long?>(),
                It.IsAny<IReadOnlyCollection<TimeSeriesLabel>>(),
                It.IsAny<bool?>(),
                It.IsAny<long?>(),
                It.IsAny<TsDuplicatePolicy?>()),
                Times.Exactly(2));

            var addParams = new TsAddParamsBuilder().Build();
            _tsCommandsMock.Verify(ts => ts.Add(
                It.Is<string>(key => key.StartsWith(_tsPrefix)),
                It.IsAny<long>(),
                It.IsAny<double>(),
                It.IsAny<long?>(),
                It.IsAny<IReadOnlyCollection<TimeSeriesLabel>>(),
                It.IsAny<bool?>(),
                It.IsAny<long?>(),
                It.IsAny<TsDuplicatePolicy?>()),
                Times.Exactly(2));
        }
    }
}
