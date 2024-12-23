using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Moq;
using NRedisStack;
using Pulsar.Runtime.Services;
using Pulsar.Runtime.Storage;
using Pulsar.Runtime.Tests.Helpers;
using Serilog;
using StackExchange.Redis;
using Xunit;

namespace Pulsar.Runtime.Tests.Storage
{
    public class RedisSensorDataProviderTests
    {
        private readonly Mock<ConnectionMultiplexer> _connection;
        private readonly Mock<IDatabase> _database;
        private readonly Mock<ILogger> _logger;
        private readonly Mock<ISensorTemporalBufferService> _temporalBuffer;
        private readonly RedisSensorDataProvider _provider;
        private readonly TestRedisServer _testServer;

        public RedisSensorDataProviderTests()
        {
            _connection = new Mock<ConnectionMultiplexer>();
            _database = new Mock<IDatabase>();
            _logger = new Mock<ILogger>();
            _temporalBuffer = new Mock<ISensorTemporalBufferService>();
            _testServer = new TestRedisServer();

            _connection
                .Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(_database.Object);

            _connection.Setup(x => x.IsConnected).Returns(true);

            _logger.Setup(l => l.ForContext<It.IsAnyType>()).Returns(_logger.Object);

            _provider = new RedisSensorDataProvider(
                _connection.Object,
                _logger.Object,
                _temporalBuffer.Object
            );
        }

        [Fact]
        public async Task SetSensorDataAsync_StoresData()
        {
            // Arrange
            var sensorId = "test-sensor";
            var value = 42.0;

            _database
                .Setup(x =>
                    x.StringSetAsync(
                        It.Is<RedisKey>(k => k.ToString().EndsWith(sensorId)),
                        It.Is<RedisValue>(v => v.ToString() == value.ToString()),
                        null,
                        When.Always,
                        CommandFlags.None
                    )
                )
                .ReturnsAsync(true);

            // Act
            await _provider.SetSensorDataAsync(sensorId, value);

            // Assert
            _database.Verify(
                x =>
                    x.StringSetAsync(
                        It.Is<RedisKey>(k => k.ToString().EndsWith(sensorId)),
                        It.Is<RedisValue>(v => v.ToString() == value.ToString()),
                        null,
                        When.Always,
                        CommandFlags.None
                    ),
                Times.Once
            );

            _temporalBuffer.Verify(x => x.AddSensorValue(sensorId, value), Times.Once);
        }

        [Fact]
        public async Task GetHistoricalDataAsync_UsesTemporalBuffer_WhenAvailable()
        {
            // Arrange
            var sensorId = "test-sensor";
            var duration = TimeSpan.FromMinutes(5);
            var historicalData = new[]
            {
                (DateTime.UtcNow.AddMinutes(-4), 42.0),
                (DateTime.UtcNow.AddMinutes(-2), 43.0),
            };

            _temporalBuffer
                .Setup(x => x.GetSensorHistory(sensorId, duration))
                .ReturnsAsync(historicalData);

            // Act
            var result = await _provider.GetHistoricalDataAsync(sensorId, duration);

            // Assert
            Assert.Equal(historicalData, result);
            _temporalBuffer.Verify(x => x.GetSensorHistory(sensorId, duration), Times.Once);
            _database.Verify(
                x => x.StringGetAsync(It.IsAny<RedisKey>(), CommandFlags.None),
                Times.Never
            );
        }

        [Fact]
        public async Task GetHistoricalDataAsync_UsesCurrentValue_WhenNoTemporalBuffer()
        {
            // Arrange
            var sensorId = "test-sensor";
            var duration = TimeSpan.FromMinutes(5);
            var currentValue = "42.0";

            _temporalBuffer
                .Setup(x => x.GetSensorHistory(sensorId, duration))
                .ReturnsAsync(Array.Empty<(DateTime, double)>());

            _database
                .Setup(x =>
                    x.StringGetAsync(
                        It.Is<RedisKey>(k => k.ToString().EndsWith(sensorId)),
                        CommandFlags.None
                    )
                )
                .ReturnsAsync(currentValue);

            // Act
            var result = await _provider.GetHistoricalDataAsync(sensorId, duration);

            // Assert
            Assert.Single(result);
            Assert.Equal(42.0, result[0].Value);
            _temporalBuffer.Verify(x => x.GetSensorHistory(sensorId, duration), Times.Once);
            _database.Verify(
                x =>
                    x.StringGetAsync(
                        It.Is<RedisKey>(k => k.ToString().EndsWith(sensorId)),
                        CommandFlags.None
                    ),
                Times.Once
            );
        }

        [Fact]
        public async Task GetHistoricalDataAsync_HandlesInvalidValue()
        {
            // Arrange
            var sensorId = "test-sensor";
            var duration = TimeSpan.FromMinutes(5);
            var invalidValue = "not-a-number";

            _temporalBuffer
                .Setup(x => x.GetSensorHistory(sensorId, duration))
                .ReturnsAsync(Array.Empty<(DateTime, double)>());

            _database
                .Setup(x =>
                    x.StringGetAsync(
                        It.Is<RedisKey>(k => k.ToString().EndsWith(sensorId)),
                        CommandFlags.None
                    )
                )
                .ReturnsAsync(invalidValue);

            // Act
            var result = await _provider.GetHistoricalDataAsync(sensorId, duration);

            // Assert
            Assert.Empty(result);
            _temporalBuffer.Verify(x => x.GetSensorHistory(sensorId, duration), Times.Once);
            _database.Verify(
                x =>
                    x.StringGetAsync(
                        It.Is<RedisKey>(k => k.ToString().EndsWith(sensorId)),
                        CommandFlags.None
                    ),
                Times.Once
            );
        }
    }
}
