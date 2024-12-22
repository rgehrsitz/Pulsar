using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Pulsar.Runtime.Configuration;
using Pulsar.Runtime.Engine;
using Serilog;

namespace Pulsar.Runtime.Services;

/// <summary>
/// Manages Pulsar's active/passive state based on Redis master location
/// </summary>
public class PulsarStateManager : BackgroundService
{
    private readonly ILogger _logger;
    private readonly RedisClusterConfiguration _clusterConfig;
    private readonly RuleEngine _ruleEngine;
    private readonly TimeSpan _checkInterval;
    private bool _isActive;

    public PulsarStateManager(
        ILogger logger,
        RedisClusterConfiguration clusterConfig,
        RuleEngine ruleEngine,
        TimeSpan? checkInterval = null
    )
    {
        _logger = logger.ForContext<PulsarStateManager>();
        _clusterConfig = clusterConfig;
        _ruleEngine = ruleEngine;
        _checkInterval = checkInterval ?? TimeSpan.FromSeconds(2);
        _isActive = false;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.Information(
            "Starting Pulsar state manager with {Interval}s check interval",
            _checkInterval.TotalSeconds
        );

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndUpdateState();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error checking Pulsar state");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task CheckAndUpdateState()
    {
        var shouldBeActive = _clusterConfig.ShouldPulsarBeActive();

        if (shouldBeActive != _isActive)
        {
            if (shouldBeActive)
            {
                _logger.Information("Activating Pulsar instance");
                await _ruleEngine.StartAsync(CancellationToken.None);
            }
            else
            {
                _logger.Information("Deactivating Pulsar instance");
                await _ruleEngine.StopAsync(CancellationToken.None);
            }

            _isActive = shouldBeActive;
        }
    }

    public bool IsActive => _isActive;
}
