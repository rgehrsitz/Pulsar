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
public class PulsarStateManager : BackgroundService, IDisposable
{
    private readonly ILogger _logger;
    private readonly RedisClusterConfiguration _redisConfig;
    private readonly RuleEngine _ruleEngine;
    private readonly TimeSpan _stateCheckInterval;
    private bool _isActive;
    private bool _disposed;

    public PulsarStateManager(
        ILogger logger,
        RedisClusterConfiguration redisConfig,
        RuleEngine ruleEngine,
        TimeSpan stateCheckInterval)
    {
        _logger = logger.ForContext<PulsarStateManager>();
        _redisConfig = redisConfig;
        _ruleEngine = ruleEngine;
        _stateCheckInterval = stateCheckInterval;
        _isActive = false;
        _disposed = false;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.Information(
            "Starting Pulsar state manager with {Interval}s check interval",
            _stateCheckInterval.TotalSeconds
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

            await Task.Delay(_stateCheckInterval, stoppingToken);
        }
    }

    private async Task CheckAndUpdateState()
    {
        var shouldBeActive = _redisConfig.ShouldPulsarBeActive();

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

    public override void Dispose()
    {
        if (!_disposed)
        {
            // ...existing cleanup code...
            _disposed = true;
        }

        base.Dispose();
    }
}
