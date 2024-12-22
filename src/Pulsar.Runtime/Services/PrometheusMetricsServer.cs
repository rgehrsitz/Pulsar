using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Prometheus;
using Serilog;

namespace Pulsar.Runtime.Services;

/// <summary>
/// Hosts a Prometheus metrics server that exposes metrics on the /metrics endpoint
/// </summary>
public class PrometheusMetricsServer : IHostedService
{
    private readonly ILogger _logger;
    private readonly MetricServer _server;
    private readonly string _host;
    private readonly int _port;
    private bool _isStarted;

    public PrometheusMetricsServer(ILogger logger, string host = "localhost", int port = 9090)
    {
        _logger = logger.ForContext<PrometheusMetricsServer>();
        _host = host;
        _port = port;
        _server = new MetricServer(_host, _port);
        _isStarted = false;

        _logger.Information("Created Prometheus metrics server on {Host}:{Port}", host, port);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _server.Start();
            _isStarted = true;
            _logger.Information(
                "Started Prometheus metrics server on http://{Host}:{Port}/metrics",
                _host,
                _port
            );
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to start Prometheus metrics server");
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_isStarted)
            {
                await _server.StopAsync();
                _isStarted = false;
                _logger.Information("Stopped Prometheus metrics server");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error stopping Prometheus metrics server");
            throw;
        }
    }
}
