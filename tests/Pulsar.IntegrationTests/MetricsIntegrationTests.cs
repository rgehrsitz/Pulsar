using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Pulsar.Runtime.Collections;
using Pulsar.Runtime.Services;
using Serilog;
using Xunit;
using Xunit.Abstractions;

namespace Pulsar.IntegrationTests;

public class MetricsIntegrationTests : IAsyncDisposable
{
    private readonly ILogger _logger;
    private readonly MetricsService _metricsService;
    private readonly TimeSeriesService _timeSeriesService;
    private readonly PrometheusMetricsServer _metricsServer;
    private readonly HttpClient _httpClient;
    private readonly string _metricsEndpoint;
    private static int _nextPort = 9091;

    public MetricsIntegrationTests(ITestOutputHelper output)
    {
        _logger = new LoggerConfiguration().WriteTo.TestOutput(output).CreateLogger();

        var port = _nextPort++;
        _metricsEndpoint = $"http://localhost:{port}/metrics";

        _metricsService = new MetricsService(_logger);
        _timeSeriesService = new TimeSeriesService(_logger, _metricsService, 100);
        _metricsServer = new PrometheusMetricsServer(_logger, "localhost", port);
        _httpClient = new HttpClient();
    }

    [Fact]
    public async Task TimeSeriesMetrics_ShouldBeRecorded()
    {
        // Arrange
        await _metricsServer.StartAsync(default);
        const string dataSource = "test_sensor";
        const double value1 = 42.0;
        const double value2 = 43.0;

        // Act
        _timeSeriesService.Update(dataSource, value1);
        _timeSeriesService.Update(dataSource, value2);

        // Wait for metrics to be collected
        await Task.Delay(100);

        // Assert
        var metricsResponse = await _httpClient.GetStringAsync(_metricsEndpoint);
        _logger.Information("Metrics response:\n{Response}", metricsResponse);

        // Check for time series metrics
        Assert.Contains(
            $"pulsar_time_series_updates_total{{data_source=\"{dataSource}\"}} 2",
            metricsResponse
        );
        Assert.Contains(
            $"pulsar_time_series_buffer_size{{data_source=\"{dataSource}\"}} 2",
            metricsResponse
        );

        // Check for sensor metrics
        Assert.Contains(
            $"pulsar_sensor_value{{sensor_name=\"{dataSource}\"}} {value2}",
            metricsResponse
        );
        Assert.Contains(
            $"pulsar_sensor_updates_total{{sensor_name=\"{dataSource}\"}} 2",
            metricsResponse
        );
    }

    [Fact]
    public async Task TimeSeriesOverflow_ShouldBeRecorded()
    {
        // Arrange
        await _metricsServer.StartAsync(default);
        const string dataSource = "test_sensor";
        const int bufferSize = 2;
        var timeSeriesService = new TimeSeriesService(_logger, _metricsService, bufferSize);

        // Act
        for (int i = 0; i < bufferSize + 1; i++)
        {
            timeSeriesService.Update(dataSource, i);
        }

        // Wait for metrics to be collected
        await Task.Delay(100);

        // Assert
        var metricsResponse = await _httpClient.GetStringAsync(_metricsEndpoint);
        Assert.Contains(
            $"pulsar_time_series_overflow_total{{data_source=\"{dataSource}\"}} 1",
            metricsResponse
        );
        Assert.Contains(
            $"pulsar_time_series_buffer_size{{data_source=\"{dataSource}\"}} 2",
            metricsResponse
        );
    }

    [Fact]
    public async Task BufferNotFound_ShouldRecordError()
    {
        // Arrange
        await _metricsServer.StartAsync(default);
        const string sensorName = "non_existent_sensor";

        // Act
        var values = _timeSeriesService.GetTimeWindow(sensorName, TimeSpan.FromMinutes(5));

        // Wait for metrics to be collected
        await Task.Delay(100);

        // Assert
        var metricsResponse = await _httpClient.GetStringAsync(_metricsEndpoint);
        Assert.Contains(
            $"pulsar_sensor_read_errors_total{{sensor_name=\"{sensorName}\",error_type=\"BufferNotFound\"}} 1",
            metricsResponse
        );
    }

    public async ValueTask DisposeAsync()
    {
        if (_metricsServer != null)
        {
            await _metricsServer.StopAsync(default);
        }
        _httpClient.Dispose();
    }
}
