using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Moq;
using StackExchange.Redis;
using Xunit;
using Pulsar.Runtime.Configuration;
using Pulsar.Runtime.Services;
using Serilog;

namespace Pulsar.Runtime.Tests.Services
{
    public class ClusterHealthServiceTests
    {
        private readonly Mock<IConnectionMultiplexer> _multiplexerMock;
        private readonly Mock<IServer> _sentinelMock;
        private readonly Mock<ILogger> _loggerMock;
        private readonly Mock<MetricsService> _metricsServiceMock;
        private readonly Mock<PulsarStateManager> _stateManagerMock;
        private readonly RedisClusterConfiguration _clusterConfig;
        private readonly string _buildingId = "test-building";

        public ClusterHealthServiceTests()
        {
            _multiplexerMock = new Mock<IConnectionMultiplexer>();
            _sentinelMock = new Mock<IServer>();
            _loggerMock = new Mock<ILogger>();
            _metricsServiceMock = new Mock<MetricsService>(_loggerMock.Object);
            _stateManagerMock = new Mock<PulsarStateManager>();
            _clusterConfig = new RedisClusterConfiguration(_loggerMock.Object, "master", new[] { "localhost:26379" }, Environment.MachineName);

            // Setup Redis connection
            var endPoint = new DnsEndPoint("localhost", 26379);
            _multiplexerMock.Setup(m => m.GetServer(endPoint))
                .Returns(_sentinelMock.Object);
            _multiplexerMock.Setup(m => m.IsConnected)
                .Returns(true);

            // Setup logger context
            _loggerMock.Setup(x => x.ForContext<ClusterHealthService>())
                .Returns(_loggerMock.Object);
        }

        [Fact]
        public async Task CheckClusterHealth_HealthyCluster_RecordsCorrectMetrics()
        {
            // Arrange
            var masterEndpoint = "127.0.0.1:6379";
            var slaveEndpoints = new[] { "127.0.0.1:6380" };

            SetupHealthyCluster(masterEndpoint, slaveEndpoints);

            var service = new ClusterHealthService(
                _loggerMock.Object,
                _clusterConfig,
                _metricsServiceMock.Object,
                _stateManagerMock.Object,
                _buildingId);

            // Act
            await service.StartAsync(default);
            await Task.Delay(100); // Allow one health check to complete
            await service.StopAsync(default);

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
                    slaveEndpoints[0],
                    true,
                    _buildingId),
                Times.Once);

            _metricsServiceMock.Verify(
                x => x.RecordPulsarStatus(
                    _buildingId,
                    It.IsAny<bool>()),
                Times.Once);
        }

        [Fact]
        public async Task CheckClusterHealth_MasterFailure_RecordsFailureMetrics()
        {
            // Arrange
            var masterEndpoint = "127.0.0.1:6379";
            var slaveEndpoints = new[] { "127.0.0.1:6380" };

            SetupClusterWithFailedMaster(masterEndpoint, slaveEndpoints);

            var service = new ClusterHealthService(
                _loggerMock.Object,
                _clusterConfig,
                _metricsServiceMock.Object,
                _stateManagerMock.Object,
                _buildingId);

            // Act
            await service.StartAsync(default);
            await Task.Delay(100); // Allow one health check to complete
            await service.StopAsync(default);

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
                    It.IsAny<string>(),
                    It.IsAny<object[]>()),
                Times.Once);
        }

        [Fact]
        public async Task CheckClusterHealth_SlaveFailure_ContinuesOperation()
        {
            // Arrange
            var masterEndpoint = "127.0.0.1:6379";
            var slaveEndpoints = new[] { "127.0.0.1:6380" };

            SetupClusterWithFailedSlave(masterEndpoint, slaveEndpoints);

            var service = new ClusterHealthService(
                _loggerMock.Object,
                _clusterConfig,
                _metricsServiceMock.Object,
                _stateManagerMock.Object,
                _buildingId);

            // Act
            await service.StartAsync(default);
            await Task.Delay(100); // Allow one health check to complete
            await service.StopAsync(default);

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
                    slaveEndpoints[0],
                    false,
                    _buildingId),
                Times.Once);

            _metricsServiceMock.Verify(
                x => x.RecordPulsarStatus(
                    _buildingId,
                    It.IsAny<bool>()),
                Times.Once);
        }

        private void SetupHealthyCluster(string masterEndpoint, string[] slaveEndpoints)
        {
            // Setup sentinel responses
            _sentinelMock.Setup(s => s.SentinelGetMasterAddressByNameAsync("master", It.IsAny<CommandFlags>()))
                .ReturnsAsync(new IPEndPoint(IPAddress.Parse(masterEndpoint.Split(':')[0]), int.Parse(masterEndpoint.Split(':')[1])));

            var replicaInfo = slaveEndpoints.Select(endpoint => new[]
            {
                new KeyValuePair<string, string>("ip", endpoint.Split(':')[0]),
                new KeyValuePair<string, string>("port", endpoint.Split(':')[1])
            }).ToArray();

            _sentinelMock.Setup(s => s.SentinelReplicasAsync("master", It.IsAny<CommandFlags>()))
                .ReturnsAsync(replicaInfo);

            // Setup master server
            var masterServer = new Mock<IServer>();
            masterServer.Setup(s => s.IsConnected).Returns(true);
            
            var replicationInfo = string.Join("\n", new[]
            {
                "role:master",
                "connected_slaves:1"
            });

            masterServer.Setup(s => s.InfoAsync(It.IsAny<string>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(replicationInfo);

            _multiplexerMock.Setup(m => m.GetServer(It.IsAny<EndPoint>()))
                .Returns(masterServer.Object);

            // Setup slave servers
            foreach (var slaveEndpoint in slaveEndpoints)
            {
                var slaveServer = new Mock<IServer>();
                slaveServer.Setup(s => s.IsConnected).Returns(true);
                
                var slaveInfo = string.Join("\n", new[]
                {
                    "role:slave",
                    $"master_host:{masterEndpoint.Split(':')[0]}"
                });

                slaveServer.Setup(s => s.InfoAsync(It.IsAny<string>(), It.IsAny<CommandFlags>()))
                    .ReturnsAsync(slaveInfo);

                _multiplexerMock.Setup(m => m.GetServer(It.IsAny<EndPoint>()))
                    .Returns(slaveServer.Object);
            }
        }

        private void SetupClusterWithFailedMaster(string masterEndpoint, string[] slaveEndpoints)
        {
            // Setup sentinel responses
            _sentinelMock.Setup(s => s.SentinelGetMasterAddressByNameAsync("master", It.IsAny<CommandFlags>()))
                .ReturnsAsync(new IPEndPoint(IPAddress.Parse(masterEndpoint.Split(':')[0]), int.Parse(masterEndpoint.Split(':')[1])));

            // Setup master server
            var masterServer = new Mock<IServer>();
            masterServer.Setup(s => s.IsConnected).Returns(false);
            masterServer.Setup(s => s.InfoAsync(It.IsAny<string>(), It.IsAny<CommandFlags>()))
                .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection failed"));

            _multiplexerMock.Setup(m => m.GetServer(It.IsAny<EndPoint>()))
                .Returns(masterServer.Object);
        }

        private void SetupClusterWithFailedSlave(string masterEndpoint, string[] slaveEndpoints)
        {
            SetupHealthyCluster(masterEndpoint, slaveEndpoints);

            // Setup slave servers
            foreach (var slaveEndpoint in slaveEndpoints)
            {
                var slaveServer = new Mock<IServer>();
                slaveServer.Setup(s => s.IsConnected).Returns(false);
                slaveServer.Setup(s => s.InfoAsync(It.IsAny<string>(), It.IsAny<CommandFlags>()))
                    .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection failed"));

                _multiplexerMock.Setup(m => m.GetServer(It.IsAny<EndPoint>()))
                    .Returns(slaveServer.Object);
            }
        }
    }
}
