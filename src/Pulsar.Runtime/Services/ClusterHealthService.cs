using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Pulsar.Runtime.Configuration;
using Serilog;

namespace Pulsar.Runtime.Services;

/// <summary>
/// Monitors Redis cluster health and reports metrics
/// </summary>
public class ClusterHealthService : BackgroundService
{
    private readonly ILogger _logger;
    private readonly RedisClusterConfiguration _clusterConfig;
    private readonly MetricsService _metrics;
    private readonly TimeSpan _checkInterval;
    private readonly string _buildingId;
    private readonly PulsarStateManager _stateManager;

    public ClusterHealthService(
        ILogger logger,
        RedisClusterConfiguration clusterConfig,
        MetricsService metrics,
        PulsarStateManager stateManager,
        string buildingId,
        TimeSpan? checkInterval = null
    )
    {
        _logger = logger.ForContext<ClusterHealthService>();
        _clusterConfig = clusterConfig;
        _metrics = metrics;
        _stateManager = stateManager;
        _buildingId = buildingId;
        _checkInterval = checkInterval ?? TimeSpan.FromSeconds(5);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.Information(
            "Starting cluster health monitoring with {Interval}s interval",
            _checkInterval.TotalSeconds
        );

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckClusterHealth();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error checking cluster health");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task CheckClusterHealth()
    {
        try
        {
            var connection = _clusterConfig.GetConnection();

            // Check master node
            var master = _clusterConfig.GetCurrentMaster();
            var masterServer = connection.GetServer(master);
            var masterInfo = await masterServer.InfoAsync();

            _metrics.RecordNodeStatus("master", master, masterServer.IsConnected, _buildingId);
            _logger.Debug(
                "Master node status: {Status} at {Endpoint} in Building {BuildingId}",
                masterServer.IsConnected ? "Connected" : "Disconnected",
                master,
                _buildingId
            );

            // Check slave nodes
            foreach (var slave in _clusterConfig.GetSlaves())
            {
                var slaveServer = connection.GetServer(slave);
                var slaveInfo = await slaveServer.InfoAsync();

                _metrics.RecordNodeStatus("slave", slave, slaveServer.IsConnected, _buildingId);
                _logger.Debug(
                    "Slave node status: {Status} at {Endpoint} in Building {BuildingId}",
                    slaveServer.IsConnected ? "Connected" : "Disconnected",
                    slave,
                    _buildingId
                );
            }

            // Record Pulsar status
            _metrics.RecordPulsarStatus(_buildingId, _stateManager.IsActive);
        }
        catch (Exception ex)
        {
            _logger.Error(
                ex,
                "Failed to check cluster health in Building {BuildingId}",
                _buildingId
            );
            throw;
        }
    }
}
