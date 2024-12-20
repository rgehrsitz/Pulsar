using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using StackExchange.Redis;
using Xunit;
using Serilog;
using Pulsar.Runtime.Services;
using Pulsar.Runtime.Configuration;
using Newtonsoft.Json.Linq;

namespace Pulsar.Runtime.Tests.Services;

public class ClusterHealthServiceTests : IDisposable
{
    private readonly Mock<ILogger> _loggerMock;
    private readonly Mock<IConnectionMultiplexer> _multiplexerMock;
    private readonly Mock<IServer> _serverMock;
    private readonly Mock<IServer> _sentinelMock;
    private readonly Mock<MetricsService> _metricsServiceMock;
    private readonly Mock<PulsarStateManager> _stateManagerMock;
    private readonly string _buildingId = "test-building";
    private readonly string _masterName = "mymaster";
    private readonly string[] _sentinelHosts = new[] { "localhost:26379" };
    private readonly string _hostname = "test-host";
    private RedisClusterConfiguration _clusterConfig;
    private ClusterHealthService _healthService;
    private CancellationTokenSource _cts;

    public ClusterHealthServiceTests()
    {
        _loggerMock = new Mock<ILogger>();
        _multiplexerMock = new Mock<IConnectionMultiplexer>();
        _serverMock = new Mock<IServer>();
        _sentinelMock = new Mock<IServer>();
        _metricsServiceMock = new Mock<MetricsService>(_loggerMock.Object);
        _stateManagerMock = new Mock<PulsarStateManager>();
        _cts = new CancellationTokenSource();

        // Setup Redis connection
        var endPoint = new DnsEndPoint("localhost", 26379);
        _multiplexerMock.Setup(m => m.GetServer(endPoint))
            .Returns(_sentinelMock.Object);
        _multiplexerMock.Setup(m => m.IsConnected)
            .Returns(true);

        // Setup logger
        _loggerMock.Setup(x => x.ForContext<ClusterHealthService>())
            .Returns(_loggerMock.Object);
        _loggerMock.Setup(x => x.ForContext<RedisClusterConfiguration>())
            .Returns(_loggerMock.Object);

        // Setup cluster configuration
        _clusterConfig = new TestRedisClusterConfiguration(
            _loggerMock.Object,
            _masterName,
            _sentinelHosts,
            _hostname,
            _multiplexerMock.Object);

        // Create health service with short check interval for testing
        _healthService = new ClusterHealthService(
            _loggerMock.Object,
            _clusterConfig,
            _metricsServiceMock.Object,
            _stateManagerMock.Object,
            _buildingId,
            TimeSpan.FromMilliseconds(100));
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _healthService?.Dispose();
        _clusterConfig?.Dispose();
    }

    [Fact]
    public async Task CheckClusterHealth_HealthyCluster_RecordsCorrectMetrics()
    {
        // Arrange
        var masterEndpoint = "10.0.0.1:6379";
        var slaveEndpoint = "10.0.0.2:6379";

        SetupHealthyCluster(masterEndpoint, new[] { slaveEndpoint });

        // Act
        await _healthService.StartAsync(_cts.Token);
        await Task.Delay(200); // Allow one health check to complete
        await _healthService.StopAsync(_cts.Token);

        // Assert
        _metricsServiceMock.Verify(
            x => x.RecordNodeStatus(
                "master",
                masterEndpoint,
                true,
                _buildingId),
            Times.Once);

        _metricsServiceMock.Verify(
            x => x.RecordNodeStatus(
                "slave",
                slaveEndpoint,
                true,
                _buildingId),
            Times.Once);

        _metricsServiceMock.Verify(
            x => x.RecordPulsarStatus(
                _buildingId,
                true),
            Times.Once);
    }

    [Fact]
    public async Task CheckClusterHealth_MasterFailure_RecordsFailureMetrics()
    {
        // Arrange
        var masterEndpoint = "10.0.0.1:6379";
        var slaveEndpoint = "10.0.0.2:6379";

        SetupClusterWithFailedMaster(masterEndpoint, new[] { slaveEndpoint });

        // Act
        await _healthService.StartAsync(_cts.Token);
        await Task.Delay(200); // Allow one health check to complete
        await _healthService.StopAsync(_cts.Token);

        // Assert
        _metricsServiceMock.Verify(
            x => x.RecordNodeStatus(
                "master",
                masterEndpoint,
                false,
                _buildingId),
            Times.Once);

        _loggerMock.Verify(
            x => x.Error(
                It.IsAny<Exception>(),
                It.Is<string>(s => s.Contains("Failed to check cluster health")),
                It.IsAny<object[]>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckClusterHealth_SlaveFailure_ContinuesOperation()
    {
        // Arrange
        var masterEndpoint = "10.0.0.1:6379";
        var slaveEndpoint = "10.0.0.2:6379";

        SetupClusterWithFailedSlave(masterEndpoint, new[] { slaveEndpoint });

        // Act
        await _healthService.StartAsync(_cts.Token);
        await Task.Delay(200); // Allow one health check to complete
        await _healthService.StopAsync(_cts.Token);

        // Assert
        _metricsServiceMock.Verify(
            x => x.RecordNodeStatus(
                "master",
                masterEndpoint,
                true,
                _buildingId),
            Times.Once);

        _metricsServiceMock.Verify(
            x => x.RecordNodeStatus(
                "slave",
                slaveEndpoint,
                false,
                _buildingId),
            Times.Once);

        _metricsServiceMock.Verify(
            x => x.RecordPulsarStatus(
                _buildingId,
                true),
            Times.Once);
    }

    private void SetupHealthyCluster(string masterEndpoint, string[] slaveEndpoints)
    {
        var masterServer = new Mock<IServer>();
        masterServer.Setup(s => s.IsConnected).Returns(true);
        masterServer.Setup(s => s.Info("replication"))
            .Returns(new[]
            {
                new KeyValuePair<string, string>("role", "master"),
                new KeyValuePair<string, string>("connected_slaves", "1")
            });

        _multiplexerMock.Setup(m => m.GetServer(masterEndpoint))
            .Returns(masterServer.Object);

        foreach (var slaveEndpoint in slaveEndpoints)
        {
            var slaveServer = new Mock<IServer>();
            slaveServer.Setup(s => s.IsConnected).Returns(true);
            slaveServer.Setup(s => s.Info("replication"))
                .Returns(new[]
                {
                    new KeyValuePair<string, string>("role", "slave"),
                    new KeyValuePair<string, string>("master_host", masterEndpoint.Split(':')[0])
                });

            _multiplexerMock.Setup(m => m.GetServer(slaveEndpoint))
                .Returns(slaveServer.Object);
        }

        // Setup sentinel responses
        var masterInfo = new JObject
        {
            ["ip"] = masterEndpoint.Split(':')[0],
            ["port"] = masterEndpoint.Split(':')[1]
        };

        _sentinelMock.Setup(s => s.SentinelMaster(_masterName))
            .Returns(masterInfo.ToString());

        var slaveInfos = slaveEndpoints.Select(endpoint => new JObject
        {
            ["ip"] = endpoint.Split(':')[0],
            ["port"] = endpoint.Split(':')[1]
        });

        _sentinelMock.Setup(s => s.SentinelSlaves(_masterName))
            .Returns(slaveInfos.Select(info => info.ToString()).ToArray());
    }

    private void SetupClusterWithFailedMaster(string masterEndpoint, string[] slaveEndpoints)
    {
        var masterServer = new Mock<IServer>();
        masterServer.Setup(s => s.IsConnected).Returns(false);
        masterServer.Setup(s => s.Info("replication"))
            .Throws(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection failed"));

        _multiplexerMock.Setup(m => m.GetServer(masterEndpoint))
            .Returns(masterServer.Object);

        // Setup sentinel responses
        var masterInfo = new JObject
        {
            ["ip"] = masterEndpoint.Split(':')[0],
            ["port"] = masterEndpoint.Split(':')[1]
        };

        _sentinelMock.Setup(s => s.SentinelMaster(_masterName))
            .Returns(masterInfo.ToString());
    }

    private void SetupClusterWithFailedSlave(string masterEndpoint, string[] slaveEndpoints)
    {
        SetupHealthyCluster(masterEndpoint, slaveEndpoints);

        foreach (var slaveEndpoint in slaveEndpoints)
        {
            var slaveServer = new Mock<IServer>();
            slaveServer.Setup(s => s.IsConnected).Returns(false);
            slaveServer.Setup(s => s.Info("replication"))
                .Throws(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection failed"));

            _multiplexerMock.Setup(m => m.GetServer(slaveEndpoint))
                .Returns(slaveServer.Object);
        }
    }
}

/// <summary>
/// Test implementation of RedisClusterConfiguration that allows injection of a mock connection
/// </summary>
public class TestRedisClusterConfiguration : RedisClusterConfiguration
{
    private readonly IConnectionMultiplexer _mockConnection;

    public TestRedisClusterConfiguration(
        ILogger logger,
        string masterName,
        string[] sentinelHosts,
        string currentHostname,
        IConnectionMultiplexer mockConnection)
        : base(logger, masterName, sentinelHosts, currentHostname)
    {
        _mockConnection = mockConnection;
    }

    public override IConnectionMultiplexer GetConnection()
    {
        return _mockConnection;
    }
}
