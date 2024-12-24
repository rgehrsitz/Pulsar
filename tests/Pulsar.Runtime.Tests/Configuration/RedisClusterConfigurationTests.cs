using System;
using System.Linq;
using System.Net;
using Moq;
using Pulsar.Runtime.Configuration;
using Serilog;
using StackExchange.Redis;
using Xunit;

namespace Pulsar.Runtime.Tests.Configuration
{
    public class RedisClusterConfigurationTests
    {
        private readonly Mock<ILogger> _loggerMock;
        private readonly string _masterNode = "localhost:26379";
        private readonly string[] _nodes = new[] { "localhost:26379", "localhost:26380" };
        private readonly string _machineName = "test-machine";

        public RedisClusterConfigurationTests()
        {
            _loggerMock = new Mock<ILogger>();
            _loggerMock
                .Setup(m => m.ForContext<RedisClusterConfiguration>())
                .Returns(_loggerMock.Object);
        }

        [Fact]
        public void Constructor_ValidParameters_Success()
        {
            // Arrange
            var mockConnectionMultiplexer = new Mock<IRedisConnectionMultiplexer>();
            var mockServer = new Mock<IServer>();
            mockServer
                .Setup(s =>
                    s.SentinelGetMasterAddressByName(It.IsAny<string>(), It.IsAny<CommandFlags>())
                )
                .Returns(new DnsEndPoint("localhost", 26379));
            mockServer
                .Setup(s =>
                    s.SentinelGetReplicaAddresses(It.IsAny<string>(), It.IsAny<CommandFlags>())
                )
                .Returns(new[] { new DnsEndPoint("localhost", 26380) });

            mockConnectionMultiplexer
                .Setup(c => c.GetServer(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<object>()))
                .Returns(mockServer.Object);
            mockConnectionMultiplexer
                .Setup(c => c.GetEndPoints(It.IsAny<bool>()))
                .Returns(new EndPoint[] { new DnsEndPoint("localhost", 26379) });
            mockConnectionMultiplexer.Setup(c => c.IsConnected).Returns(true);

            var config = new RedisClusterConfiguration(
                _loggerMock.Object,
                _masterNode,
                _nodes,
                _machineName,
                connection: mockConnectionMultiplexer.Object
            );

            // Act
            var master = config.GetCurrentMaster();
            var slaves = config.GetSlaves();

            // Assert
            Assert.NotNull(config);
            Assert.Equal("localhost:26379", master);
            Assert.Single(slaves);
        }

        [Fact]
        public void Constructor_NullNodes_ThrowsArgumentException()
        {
            // Arrange
            var logger = new Mock<ILogger>();
            logger.Setup(l => l.ForContext<It.IsAnyType>()).Returns(logger.Object);

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(
                () =>
                    new RedisClusterConfiguration(
                        logger.Object,
                        "master",
                        null!,
                        Environment.MachineName
                    )
            );

            Assert.Equal("sentinelHosts", ex.ParamName);
        }

        [Fact]
        public void Constructor_EmptyNodes_ThrowsArgumentException()
        {
            // Arrange
            var logger = new Mock<ILogger>();
            logger.Setup(l => l.ForContext<It.IsAnyType>()).Returns(logger.Object);

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(
                () =>
                    new RedisClusterConfiguration(
                        logger.Object,
                        "master",
                        Array.Empty<string>(),
                        Environment.MachineName
                    )
            );

            Assert.Equal("sentinelHosts", ex.ParamName);
        }

        [Fact]
        public void Constructor_NullMasterNode_ThrowsArgumentException()
        {
            // Arrange
            var logger = new Mock<ILogger>();
            logger.Setup(l => l.ForContext<It.IsAnyType>()).Returns(logger.Object);
            string? masterName = null;

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(
                () =>
                    new RedisClusterConfiguration(
                        logger.Object,
                        masterName!,
                        new[] { "localhost:6379" },
                        Environment.MachineName
                    )
            );

            Assert.Equal("masterName", ex.ParamName);
        }

        [Fact]
        public void Constructor_EmptyMasterNode_ThrowsArgumentException()
        {
            // Arrange
            var logger = new Mock<ILogger>();
            logger.Setup(l => l.ForContext<It.IsAnyType>()).Returns(logger.Object);

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(
                () =>
                    new RedisClusterConfiguration(
                        logger.Object,
                        "",
                        new[] { "localhost:6379" },
                        Environment.MachineName
                    )
            );

            Assert.Equal("masterName", ex.ParamName);
        }

        [Fact]
        public void Constructor_NullMachineName_ThrowsArgumentException()
        {
            // Arrange
            var logger = new Mock<ILogger>();
            logger.Setup(l => l.ForContext<It.IsAnyType>()).Returns(logger.Object);

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(
                () =>
                    new RedisClusterConfiguration(
                        logger.Object,
                        "master",
                        new[] { "localhost:6379" },
                        null!
                    )
            );

            Assert.Equal("currentHostname", ex.ParamName);
        }

        [Fact]
        public void Constructor_EmptyMachineName_ThrowsArgumentException()
        {
            // Arrange
            var logger = new Mock<ILogger>();
            logger.Setup(l => l.ForContext<It.IsAnyType>()).Returns(logger.Object);

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(
                () =>
                    new RedisClusterConfiguration(
                        logger.Object,
                        "master",
                        new[] { "localhost:6379" },
                        ""
                    )
            );

            Assert.Equal("currentHostname", ex.ParamName);
        }

        [Fact]
        public void Constructor_NullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(
                () =>
                    new RedisClusterConfiguration(
                        null!,
                        "master",
                        new[] { "localhost:6379" },
                        Environment.MachineName
                    )
            );

            Assert.Equal("logger", ex.ParamName);
        }
    }
}
