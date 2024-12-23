using System;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using Pulsar.Runtime.Services;
using Serilog;
using Xunit;

namespace Pulsar.Runtime.Tests.Services
{
    public class SensorTemporalBufferServiceTests
    {
        private readonly Mock<ILogger> _logger;
        private readonly SensorTemporalBufferService _service;

        public SensorTemporalBufferServiceTests()
        {
            _logger = new Mock<ILogger>();
            _logger.Setup(l => l.ForContext<It.IsAnyType>()).Returns(_logger.Object);

            _service = new SensorTemporalBufferService(_logger.Object);
        }

        [Fact]
        public async Task AddSensorValue_AddsValueToBuffer()
        {
            // Arrange
            var sensorId = "test-sensor";
            var value = 42.0;

            // Act
            await _service.AddSensorValue(sensorId, value);
            var history = await _service.GetSensorHistory(sensorId, TimeSpan.FromSeconds(1));

            // Assert
            var values = history.ToList();
            Assert.Single(values);
            Assert.Equal(value, values[0].Value);
        }

        [Fact]
        public async Task GetSensorHistory_ReturnsEmptyArray_WhenNoData()
        {
            // Arrange
            var sensorId = "test-sensor";
            var duration = TimeSpan.FromSeconds(1);

            // Act
            var result = await _service.GetSensorHistory(sensorId, duration);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetSensorHistory_ReturnsValuesWithinDuration()
        {
            // Arrange
            var sensorId = "test-sensor";
            var duration = TimeSpan.FromSeconds(5);

            // Add values
            await _service.AddSensorValue(sensorId, 41.0);
            await _service.AddSensorValue(sensorId, 42.0);
            await _service.AddSensorValue(sensorId, 43.0);

            // Act
            var result = await _service.GetSensorHistory(sensorId, duration);

            // Assert
            var values = result.ToList();
            Assert.Equal(3, values.Count);
            Assert.Equal(41.0, values[0].Value);
            Assert.Equal(42.0, values[1].Value);
            Assert.Equal(43.0, values[2].Value);
        }

        [Fact]
        public async Task GetSensorHistory_RemovesOldValues()
        {
            // Arrange
            var sensorId = "test-sensor";
            var duration = TimeSpan.FromMilliseconds(1);

            // Add a value and wait
            await _service.AddSensorValue(sensorId, 42.0);
            await Task.Delay(10); // Wait longer than the duration

            // Act
            var result = await _service.GetSensorHistory(sensorId, duration);

            // Assert
            Assert.Empty(result);
        }
    }
}
