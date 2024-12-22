// Filename: RedisSensorDataProviderTests.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using Xunit;
using Pulsar.Runtime.Storage;
using StackExchange.Redis;
using NRedisStack;
using NRedisStack.RedisStackCommands;
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
        private readonly Mock<ILogger> _loggerMock;
        private readonly RedisSensorDataProvider _provider;

        private const string _keyPrefix = "sensor:";
        private const string _tsPrefix = "ts:";

        public RedisSensorDataProviderTests()
        {
            _multiplexerMock = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);
            _databaseMock = new Mock<IDatabase>(MockBehavior.Strict);
            _tsCommandsMock = new Mock<TimeSeriesCommands>(MockBehavior.Strict);
            _loggerMock = new Mock<ILogger>(MockBehavior.Loose);

            // When production calls _connection.GetDatabase(), we return _databaseMock
            _multiplexerMock
                .Setup(m => m.GetDatabase(It.IsAny<int>(), null))
                .Returns(_databaseMock.Object);

            // We'll also mock out GetServerAsync(...) so we can control the KeysAsync(...) call
            var serverMock = new Mock<IServer>(MockBehavior.Strict);
            _multiplexerMock
                .Setup(m => m.GetServerAsync("localhost", 6379, null))
                .ReturnsAsync(serverMock.Object);

            // The server will return whatever keys we want when enumerating
            // For general tests, we'll set up a default empty result. We can override in a test if needed.
            serverMock
                .Setup(s => s.KeysAsync(
                    It.IsAny<int>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<CommandFlags>()))
                .Returns((int dbIndex, RedisValue pattern, int pageSize, int pageOffset, CommandFlags flags) =>
                    AsyncEnumerable.Empty<RedisKey>());

            // Mock TS() -> returns our _tsCommandsMock
            _databaseMock
                .Setup(d => d.TS())
                .Returns(_tsCommandsMock.Object);

            // By default, create/add just succeed
            _tsCommandsMock
                .Setup(ts => ts.CreateAsync(It.IsAny<string>(), It.IsAny<TsCreateParams>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            _tsCommandsMock
                .Setup(ts => ts.AddAsync(
                    It.IsAny<string>(),
                    It.IsAny<TimeStamp>(),
                    It.IsAny<double>(),
                    It.IsAny<TsAddParams>(),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(new TimeStamp(1));

            // RangeAsync defaults to empty
            _tsCommandsMock
                .Setup(ts => ts.RangeAsync(
                    It.IsAny<string>(),
                    It.IsAny<long>(),
                    It.IsAny<long>(),
                    It.IsAny<TsRangeParams>(),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(new List<TimeSeriesTuple>() as IReadOnlyList<TimeSeriesTuple>);

            // Create our provider with the mocks
            _provider = new RedisSensorDataProvider(
                _multiplexerMock.Object,
                _loggerMock.Object,
                _keyPrefix,
                _tsPrefix);
        }

        [Fact]
        public async Task GetCurrentDataAsync_ReturnsData()
        {
            // Arrange
            var keys = new RedisKey[] { $"{_keyPrefix}temp1", $"{_keyPrefix}temp2" };
            var values = new RedisValue[] { "25.0", "30.0" };

            // Mock out keys
            var server = _multiplexerMock.Object.GetServer("localhost", 6379);
            var serverMock = Mock.Get(server);
            serverMock
                .Setup(s => s.KeysAsync(
                    It.IsAny<int>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<CommandFlags>()))
                .Returns((int dbIndex, RedisValue pattern, int pageSize, int pageOffset, CommandFlags flags) =>
                    keys.ToAsyncEnumerable());

            // Mock out the StringGetAsync
            _databaseMock
                .Setup(d => d.StringGetAsync(It.Is<RedisKey[]>(rka => rka.SequenceEqual(keys)), It.IsAny<CommandFlags>()))
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
        public async Task GetHistoricalDataAsync_ReturnsData()
        {
            // Arrange
            var now = DateTimeOffset.UtcNow;
            var sensorName = "temp1";
            var duration = TimeSpan.FromHours(1);
            var expectedTuples = new List<TimeSeriesTuple>
            {
                new TimeSeriesTuple(now.AddMinutes(-30).ToUnixTimeMilliseconds(), 25.0),
                new TimeSeriesTuple(now.ToUnixTimeMilliseconds(), 26.0)
            } as IReadOnlyList<TimeSeriesTuple>;

            // Mock out RangeAsync
            _tsCommandsMock
                .Setup(ts => ts.RangeAsync(
                    $"{_tsPrefix}{sensorName}",
                    It.IsAny<long>(),
                    It.IsAny<long>(),
                    It.IsAny<TsRangeParams>(),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(expectedTuples);

            // Act
            var result = await _provider.GetHistoricalDataAsync(sensorName, duration);

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
            _databaseMock
                .Setup(d => d.CreateBatch())
                .Returns(batchMock.Object);

            // We just say every StringSetAsync in the batch returns true
            batchMock
                .Setup(b => b.StringSetAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    null,
                    When.Always,
                    CommandFlags.None))
                .Returns(Task.FromResult(true));

            // Act
            await _provider.SetSensorDataAsync(values);

            // Assert
            // Verify we created each timeseries
            _tsCommandsMock.Verify(ts =>
                ts.CreateAsync(
                    It.Is<string>(key => key.StartsWith(_tsPrefix)),
                    It.IsAny<TsCreateParams>(),
                    It.IsAny<CommandFlags>()),
                Times.Exactly(2));

            // Verify we added each data point
            _tsCommandsMock.Verify(ts =>
                ts.AddAsync(
                    It.Is<string>(key => key.StartsWith(_tsPrefix)),
                    It.IsAny<TimeStamp>(),
                    It.IsAny<double>(),
                    It.IsAny<TsAddParams>(),
                    It.IsAny<CommandFlags>()),
                Times.Exactly(2));
        }

        [Fact]
        public async Task GetCurrentDataAsync_ConnectionFailure_ThrowsException()
        {
            // Arrange: force a connection exception
            _databaseMock
                .Setup(d => d.StringGetAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()))
                .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Fail"));

            // Act & Assert
            await Assert.ThrowsAsync<RedisConnectionException>(() => _provider.GetCurrentDataAsync());
        }

        [Fact]
        public async Task GetHistoricalDataAsync_ConnectionFailure_ThrowsException()
        {
            // Arrange: force a connection exception on RangeAsync
            _tsCommandsMock
                .Setup(ts => ts.RangeAsync(
                    It.IsAny<string>(),
                    It.IsAny<long>(),
                    It.IsAny<long>(),
                    It.IsAny<TsRangeParams>(),
                    It.IsAny<CommandFlags>()))
                .Throws(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Fail"));

            // Act & Assert
            await Assert.ThrowsAsync<RedisConnectionException>(() =>
                _provider.GetHistoricalDataAsync("temp1", TimeSpan.FromHours(1)));
        }

        [Fact]
        public async Task GetHistoricalDataAsync_NoData_ReturnsEmptyList()
        {
            // Arrange
            var emptyList = new List<TimeSeriesTuple>() as IReadOnlyList<TimeSeriesTuple>;
            _tsCommandsMock
                .Setup(ts => ts.RangeAsync(
                    It.IsAny<string>(),
                    It.IsAny<long>(),
                    It.IsAny<long>(),
                    It.IsAny<TsRangeParams>(),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(emptyList);

            // Act
            var result = await _provider.GetHistoricalDataAsync("temp1", TimeSpan.FromHours(1));

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task SetSensorDataAsync_ValidData_Success()
        {
            // Arrange
            var values = new Dictionary<string, object>
            {
                { "temp1", 25.0 },
                { "temp2", 26.0 }
            };

            // We will just rely on default setups that return success for CreateAsync/AddAsync

            // Act
            await _provider.SetSensorDataAsync(values);

            // Assert
            // We check that the series got created and data got added
            _tsCommandsMock.Verify(ts =>
                ts.CreateAsync(
                    It.Is<string>(key => key.StartsWith(_tsPrefix)),
                    It.IsAny<TsCreateParams>(),
                    It.IsAny<CommandFlags>()),
                Times.Exactly(2));

            _tsCommandsMock.Verify(ts =>
                ts.AddAsync(
                    It.Is<string>(key => key.StartsWith(_tsPrefix)),
                    It.IsAny<TimeStamp>(),
                    It.IsAny<double>(),
                    It.IsAny<TsAddParams>(),
                    It.IsAny<CommandFlags>()),
                Times.Exactly(2));
        }
    }
}
