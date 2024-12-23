using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Moq;
using Pulsar.Runtime.Services;
using Pulsar.Runtime.Storage;
using Serilog;
using StackExchange.Redis;
using Xunit;

namespace Pulsar.Runtime.Tests.Storage
{
    public class RedisSensorDataProviderTests
    {
        private readonly Mock<IConnectionMultiplexer> _multiplexerMock;
        private readonly Mock<IDatabase> _databaseMock;
        private readonly Mock<ILogger> _loggerMock;
        private readonly Mock<SensorTemporalBufferService> _temporalBufferMock;
        private readonly RedisSensorDataProvider _provider;

        private const string _keyPrefix = "sensor:";

        public RedisSensorDataProviderTests()
        {
            _multiplexerMock = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);
            _databaseMock = new Mock<IDatabase>(MockBehavior.Strict);
            _loggerMock = new Mock<ILogger>(MockBehavior.Loose);
            _temporalBufferMock = new Mock<SensorTemporalBufferService>(MockBehavior.Strict);

            // When production calls _connection.GetDatabase(), we return _databaseMock
            _multiplexerMock
                .Setup(m => m.GetDatabase(-1, null))
                .Returns(_databaseMock.Object);

            // We'll also mock out GetServer(...) so we can control the KeysAsync(...) call
            var serverMock = new Mock<IServer>(MockBehavior.Strict);
            var serverEndpoint = new DnsEndPoint("localhost", 6379);
            _multiplexerMock
                .Setup(m => m.GetServer(serverEndpoint, CommandFlags.None))
                .Returns(serverMock.Object);

            _provider = new RedisSensorDataProvider(
                _multiplexerMock.Object,
                _loggerMock.Object,
                _temporalBufferMock.Object,
                _keyPrefix
            );

            // Setup common temporal buffer behavior
            _temporalBufferMock
                .Setup(t => t.HasTemporalBuffer(It.IsAny<string>()))
                .Returns(false);
        }

        [Fact]
        public async Task SetSensorDataAsync_StoresData()
        {
            // Arrange
            var values = new Dictionary<string, object>
            {
                { "sensor1", 42.0 },
                { "sensor2", "test" }
            };

            var batch = new Mock<IBatch>(MockBehavior.Strict);
            _databaseMock.Setup(d => d.CreateBatch(null)).Returns(batch.Object);
            
            batch.Setup(b => b.StringSetAsync($"{_keyPrefix}sensor1", "42", null, When.Always, CommandFlags.None))
                .ReturnsAsync(true);
            batch.Setup(b => b.StringSetAsync($"{_keyPrefix}sensor2", "test", null, When.Always, CommandFlags.None))
                .ReturnsAsync(true);
            batch.Setup(b => b.Execute());

            // Setup temporal buffer mock for sensor1
            _temporalBufferMock
                .Setup(t => t.HasTemporalBuffer("sensor1"))
                .Returns(true);
            _temporalBufferMock
                .Setup(t => t.UpdateSensor("sensor1", 42.0, It.IsAny<DateTime>()));

            // Act
            await _provider.SetSensorDataAsync(values);

            // Assert
            batch.VerifyAll();
            _temporalBufferMock.Verify(
                t => t.UpdateSensor("sensor1", 42.0, It.IsAny<DateTime>()),
                Times.Once
            );
        }

        [Fact]
        public async Task GetHistoricalDataAsync_UsesTemporalBuffer_WhenAvailable()
        {
            // Arrange
            var sensorName = "sensor1";
            var duration = TimeSpan.FromSeconds(1);
            var historicalData = new[]
            {
                (DateTime.UtcNow.AddSeconds(-1), 41.0),
                (DateTime.UtcNow, 42.0)
            };

            _temporalBufferMock
                .Setup(t => t.HasTemporalBuffer(sensorName))
                .Returns(true);
            _temporalBufferMock
                .Setup(t => t.GetSensorHistory(sensorName, duration))
                .Returns(historicalData);

            // Act
            var result = await _provider.GetHistoricalDataAsync(sensorName, duration);

            // Assert
            Assert.Equal(historicalData, result);
            _databaseMock.Verify(
                d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()),
                Times.Never
            );
        }

        [Fact]
        public async Task GetHistoricalDataAsync_UsesCurrentValue_WhenNoTemporalBuffer()
        {
            // Arrange
            var sensorName = "sensor1";
            var duration = TimeSpan.FromSeconds(1);
            var currentValue = "42.0";

            _temporalBufferMock
                .Setup(t => t.HasTemporalBuffer(sensorName))
                .Returns(false);
            _databaseMock
                .Setup(d => d.StringGetAsync($"{_keyPrefix}{sensorName}", CommandFlags.None))
                .ReturnsAsync(currentValue);

            // Act
            var result = await _provider.GetHistoricalDataAsync(sensorName, duration);

            // Assert
            Assert.Single(result);
            Assert.Equal(42.0, result[0].Value);
        }

        [Fact]
        public async Task GetHistoricalDataAsync_HandlesInvalidValue()
        {
            // Arrange
            var sensorName = "sensor1";
            var duration = TimeSpan.FromSeconds(1);

            _temporalBufferMock
                .Setup(t => t.HasTemporalBuffer(sensorName))
                .Returns(false);
            _databaseMock
                .Setup(d => d.StringGetAsync($"{_keyPrefix}{sensorName}", CommandFlags.None))
                .ReturnsAsync("not a number");

            // Act
            var result = await _provider.GetHistoricalDataAsync(sensorName, duration);

            // Assert
            Assert.Empty(result);
        }
    }
}
