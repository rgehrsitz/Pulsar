using System;
using System.Linq;
using Moq;
using Pulsar.Runtime.Services;
using Serilog;
using Xunit;

namespace Pulsar.Runtime.Tests.Services
{
    public class SensorTemporalBufferServiceTests
    {
        private readonly Mock<ILogger> _loggerMock;
        private readonly Mock<IMetricsService> _metricsMock;
        private readonly SensorTemporalBufferService _service;

        public SensorTemporalBufferServiceTests()
        {
            _loggerMock = new Mock<ILogger>(MockBehavior.Loose);
            _loggerMock.Setup(l => l.ForContext<SensorTemporalBufferService>()).Returns(_loggerMock.Object);
            _metricsMock = new Mock<IMetricsService>(MockBehavior.Loose);
            _service = new SensorTemporalBufferService(
                _loggerMock.Object,
                _metricsMock.Object,
                defaultBufferCapacity: 5,
                maxBufferDuration: TimeSpan.FromSeconds(2)
            );
        }

        [Fact]
        public void UpdateSensor_CreatesBuffer_WhenNotExists()
        {
            // Act
            _service.UpdateSensor("test1", 42.0);

            // Assert
            Assert.True(_service.HasTemporalBuffer("test1"));
        }

        [Fact]
        public void GetSensorHistory_ReturnsEmpty_WhenNoBuffer()
        {
            // Act
            var result = _service.GetSensorHistory("nonexistent", TimeSpan.FromSeconds(1));

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void GetSensorHistory_ReturnsData_WithinTimeWindow()
        {
            // Arrange
            var now = DateTime.UtcNow;
            _service.UpdateSensor("test1", 1.0, now.AddSeconds(-2));
            _service.UpdateSensor("test1", 2.0, now.AddSeconds(-1));
            _service.UpdateSensor("test1", 3.0, now);

            // Act
            var result = _service.GetSensorHistory("test1", TimeSpan.FromSeconds(1.5));

            // Assert
            Assert.Equal(2, result.Length);
            Assert.Equal(2.0, result[0].Value);
            Assert.Equal(3.0, result[1].Value);
        }

        [Fact]
        public void GetSensorHistory_LimitsToMaxDuration()
        {
            // Arrange
            var now = DateTime.UtcNow;
            _service.UpdateSensor("test1", 1.0, now.AddSeconds(-3));
            _service.UpdateSensor("test1", 2.0, now.AddSeconds(-1));

            // Act - Request 3 seconds but service is configured for max 2 seconds
            var result = _service.GetSensorHistory("test1", TimeSpan.FromSeconds(3));

            // Assert
            Assert.Single(result);
            Assert.Equal(2.0, result[0].Value);
            _loggerMock.Verify(
                l => l.Warning(
                    It.Is<string>(s => s.Contains("exceeds max buffer duration")),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<TimeSpan>()
                ),
                Times.Once
            );
        }

        [Fact]
        public void RemoveSensor_ClearsBuffer()
        {
            // Arrange
            _service.UpdateSensor("test1", 42.0);
            Assert.True(_service.HasTemporalBuffer("test1"));

            // Act
            _service.RemoveSensor("test1");

            // Assert
            Assert.False(_service.HasTemporalBuffer("test1"));
            Assert.Empty(_service.GetSensorHistory("test1", TimeSpan.FromSeconds(1)));
        }
    }
}
