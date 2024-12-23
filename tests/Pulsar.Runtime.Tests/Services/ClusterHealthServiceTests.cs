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
        private readonly Mock<IServer> _serverMock;
        private readonly Mock<ILogger> _loggerMock;
        private readonly Mock<MetricsService> _metricsServiceMock;
        private readonly Mock<PulsarStateManager> _stateManagerMock;
        private readonly RedisClusterConfiguration _clusterConfig;
        private readonly string _buildingId = "test-building";

        public ClusterHealthServiceTests()
        {
            _multiplexerMock = new Mock<IConnectionMultiplexer>();
            _sentinelMock = new Mock<IServer>();
            _serverMock = new Mock<IServer>();
            _loggerMock = new Mock<ILogger>();
            _metricsServiceMock = new Mock<MetricsService>(_loggerMock.Object);
            _stateManagerMock = new Mock<PulsarStateManager>();
            _clusterConfig = new RedisClusterConfiguration(_loggerMock.Object, "master", new[] { "localhost:26379" }, Environment.MachineName);

            // Setup Redis connection
            var endPoint = new DnsEndPoint("localhost", 26379);
            _multiplexerMock.Setup(m => m.GetServer(endPoint, CommandFlags.None))
                .Returns(_sentinelMock.Object);
            _multiplexerMock.Setup(m => m.IsConnected)
                .Returns(true);

            // Setup logger context
            _loggerMock.Setup(x => x.ForContext<ClusterHealthService>())
                .Returns(_loggerMock.Object);

            // Setup server info responses
            var serverInfo = new[]
            {
                new TestGrouping<string, KeyValuePair<string, string>>(
                    "Server",
                    new[]
                    {
                        new KeyValuePair<string, string>("redis_version", "6.2.0"),
                        new KeyValuePair<string, string>("uptime_in_seconds", "3600")
                    })
            };

            _serverMock.Setup(s => s.Info("server", It.IsAny<CommandFlags>()))
                .Returns(() => Task.FromResult<IGrouping<string, KeyValuePair<string, string>>[]>(serverInfo));

            _serverMock.Setup(s => s.ConfigGet("maxmemory", It.IsAny<CommandFlags>()))
                .Returns(() => Task.FromResult(new[] { new KeyValuePair<string, string>("maxmemory", "4gb") }));

            // Setup error case
            var errorServerMock = new Mock<IServer>();
            errorServerMock.Setup(s => s.Info("server", It.IsAny<CommandFlags>()))
                .Returns(() => Task.FromException<IGrouping<string, KeyValuePair<string, string>>[]>(
                    new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection failed")));

            errorServerMock.Setup(s => s.ConfigGet("maxmemory", It.IsAny<CommandFlags>()))
                .Returns(() => Task.FromException<KeyValuePair<string, string>[]>(
                    new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection failed")));
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
            _sentinelMock.Setup(s => s.SentinelGetMasterAddressByNameAsync("master", CommandFlags.None))
                .Returns(() => Task.FromResult<EndPoint>(new IPEndPoint(IPAddress.Parse(masterEndpoint.Split(':')[0]), int.Parse(masterEndpoint.Split(':')[1]))));

            var replicaInfo = slaveEndpoints.Select(endpoint => new[]
            {
                new KeyValuePair<string, string>("ip", endpoint.Split(':')[0]),
                new KeyValuePair<string, string>("port", endpoint.Split(':')[1])
            }).ToArray();

            _sentinelMock.Setup(s => s.SentinelReplicasAsync("master", CommandFlags.None))
                .Returns(() => Task.FromResult(replicaInfo));

            // Setup master server
            var masterServer = new Mock<IServer>();
            masterServer.Setup(s => s.IsConnected).Returns(true);

            var replicationInfo = new[]
            {
                new TestGrouping<string, KeyValuePair<string, string>>(
                    "Replication",
                    new[]
                    {
                        new KeyValuePair<string, string>("role", "master"),
                        new KeyValuePair<string, string>("connected_slaves", slaveEndpoints.Length.ToString())
                    })
            };

            masterServer.Setup(s => s.InfoAsync("replication", CommandFlags.None))
                .Returns(() => Task.FromResult<IGrouping<string, KeyValuePair<string, string>>[]>(replicationInfo));

            _multiplexerMock.Setup(m => m.GetServer(It.IsAny<EndPoint>(), CommandFlags.None))
                .Returns(masterServer.Object);

            // Setup slave servers
            foreach (var slaveEndpoint in slaveEndpoints)
            {
                var slaveServer = new Mock<IServer>();
                slaveServer.Setup(s => s.IsConnected).Returns(true);

                var slaveInfo = new[]
                {
                    new TestGrouping<string, KeyValuePair<string, string>>(
                        "Replication",
                        new[]
                        {
                            new KeyValuePair<string, string>("role", "slave"),
                            new KeyValuePair<string, string>("master_host", masterEndpoint.Split(':')[0]),
                            new KeyValuePair<string, string>("master_port", masterEndpoint.Split(':')[1])
                        })
                };

                slaveServer.Setup(s => s.InfoAsync(It.IsAny<string>(), CommandFlags.None))
                    .Returns(() => Task.FromResult<IGrouping<string, KeyValuePair<string, string>>[]>(slaveInfo));

                _multiplexerMock.Setup(m => m.GetServer(It.IsAny<EndPoint>(), CommandFlags.None))
                    .Returns(slaveServer.Object);
            }
        }

        private void SetupClusterWithFailedMaster(string masterEndpoint, string[] slaveEndpoints)
        {
            // Setup sentinel responses
            _sentinelMock.Setup(s => s.SentinelGetMasterAddressByNameAsync("master", CommandFlags.None))
                .Returns(() => Task.FromResult<EndPoint>(new IPEndPoint(IPAddress.Parse(masterEndpoint.Split(':')[0]), int.Parse(masterEndpoint.Split(':')[1]))));

            // Setup master server
            var masterServer = new Mock<IServer>();
            masterServer.Setup(s => s.IsConnected).Returns(false);
            masterServer.Setup(s => s.InfoAsync(It.IsAny<string>(), CommandFlags.None))
                .Returns(() => Task.FromException<IGrouping<string, KeyValuePair<string, string>>[]>(
                    new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection failed")));

            _multiplexerMock.Setup(m => m.GetServer(It.IsAny<EndPoint>(), CommandFlags.None))
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
                slaveServer.Setup(s => s.InfoAsync(It.IsAny<string>(), CommandFlags.None))
                    .Returns(() => Task.FromException<IGrouping<string, KeyValuePair<string, string>>[]>(
                        new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection failed")));

                _multiplexerMock.Setup(m => m.GetServer(It.IsAny<EndPoint>(), CommandFlags.None))
                    .Returns(slaveServer.Object);
            }
        }
    }

    internal class TestGrouping<TKey, TElement> : IGrouping<TKey, TElement>
    {
        private readonly TKey _key;
        private readonly IEnumerable<TElement> _elements;

        public TestGrouping(TKey key, IEnumerable<TElement> elements)
        {
            _key = key;
            _elements = elements;
        }

        public TKey Key => _key;

        public IEnumerator<TElement> GetEnumerator() => _elements.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
