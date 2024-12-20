using System;
using System.Linq;
using System.Net;
using Moq;
using Serilog;
using Xunit;
using Pulsar.Runtime.Configuration;

namespace Pulsar.Runtime.Tests.Configuration;

public class RedisClusterConfigurationTests
{
    private readonly Mock<ILogger> _loggerMock;
    private readonly string _masterNode = "localhost:26379";
    private readonly string[] _nodes = new[] { "localhost:26379", "localhost:26380" };
    private readonly string _machineName = "test-machine";

    public RedisClusterConfigurationTests()
    {
        _loggerMock = new Mock<ILogger>();
        _loggerMock.Setup(m => m.ForContext<RedisClusterConfiguration>())
            .Returns(_loggerMock.Object);
    }

    [Fact]
    public void Constructor_ValidParameters_Success()
    {
        // Act
        var config = new RedisClusterConfiguration(_loggerMock.Object, _masterNode, _nodes, _machineName);

        // Assert
        Assert.NotNull(config);
        Assert.Equal(_masterNode, config.GetCurrentMaster());
        Assert.Equal(_nodes.Count() - 1, config.GetSlaves().Count());
    }

    [Fact]
    public void Constructor_NullNodes_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new RedisClusterConfiguration(_loggerMock.Object, _masterNode, null!, _machineName));
    }

    [Fact]
    public void Constructor_EmptyNodes_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new RedisClusterConfiguration(_loggerMock.Object, _masterNode, Array.Empty<string>(), _machineName));
    }

    [Fact]
    public void Constructor_NullMasterNode_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new RedisClusterConfiguration(_loggerMock.Object, null!, _nodes, _machineName));
    }

    [Fact]
    public void Constructor_EmptyMasterNode_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new RedisClusterConfiguration(_loggerMock.Object, string.Empty, _nodes, _machineName));
    }

    [Fact]
    public void Constructor_NullMachineName_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new RedisClusterConfiguration(_loggerMock.Object, _masterNode, _nodes, null!));
    }

    [Fact]
    public void Constructor_EmptyMachineName_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new RedisClusterConfiguration(_loggerMock.Object, _masterNode, _nodes, string.Empty));
    }
}
