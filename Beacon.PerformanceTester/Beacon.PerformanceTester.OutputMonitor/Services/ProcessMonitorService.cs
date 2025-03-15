using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Beacon.PerformanceTester.OutputMonitor.Services
{
    /// <summary>
    /// Service for monitoring process performance metrics
    /// </summary>
    public class ProcessMonitorService : IProcessMonitorService
    {
        private readonly ILogger<ProcessMonitorService> _logger;
        private string _processName = string.Empty;
        private Process? _targetProcess;
        private readonly List<(DateTime Timestamp, double CpuPercent, double MemoryMB)> _metrics =
            new();
        private bool _isMonitoring;
        private Task? _monitoringTask;
        private CancellationTokenSource? _cts;

        public ProcessMonitorService(ILogger<ProcessMonitorService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Initialize the process monitor
        /// </summary>
        public Task InitializeAsync(string processName)
        {
            _processName = processName;
            _logger.LogInformation(
                "Initializing process monitor for process: {ProcessName}",
                _processName
            );

            try
            {
                // Find the process by name
                var processes = Process.GetProcessesByName(_processName);
                if (processes.Length == 0)
                {
                    _logger.LogWarning("No processes found with name: {ProcessName}", _processName);
                    return Task.CompletedTask;
                }

                // If multiple processes, use the one with the highest CPU
                _targetProcess = processes
                    .OrderByDescending(p => p.TotalProcessorTime.TotalMilliseconds)
                    .First();
                _logger.LogInformation(
                    "Found process {ProcessName} with PID {PID}",
                    _processName,
                    _targetProcess.Id
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to initialize process monitor for {ProcessName}",
                    _processName
                );
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Start monitoring process metrics
        /// </summary>
        public Task StartMonitoringAsync(CancellationToken cancellationToken)
        {
            if (_targetProcess == null)
            {
                _logger.LogWarning("Cannot start monitoring because target process is not found");
                return Task.CompletedTask;
            }

            if (_isMonitoring)
            {
                _logger.LogWarning("Process monitoring is already in progress");
                return Task.CompletedTask;
            }

            _metrics.Clear();
            _isMonitoring = true;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Create a CPU counter to measure processor time
            var startTime = DateTime.UtcNow;
            var startCpuTime = _targetProcess.TotalProcessorTime;

            // Start monitoring task
            _monitoringTask = Task.Run(
                async () =>
                {
                    try
                    {
                        _logger.LogInformation(
                            "Starting process monitoring for {ProcessName} (PID: {PID})",
                            _processName,
                            _targetProcess.Id
                        );

                        // Monitor metrics every 1 second
                        while (!_cts.Token.IsCancellationRequested && _isMonitoring)
                        {
                            try
                            {
                                // Refresh process data
                                _targetProcess.Refresh();

                                // Calculate CPU usage (percentage across all cores)
                                var currentTime = DateTime.UtcNow;
                                var currentCpuTime = _targetProcess.TotalProcessorTime;
                                var cpuTimeUsed = (currentCpuTime - startCpuTime).TotalMilliseconds;
                                var totalTimeElapsed = (currentTime - startTime).TotalMilliseconds;
                                var cpuUsagePercent =
                                    cpuTimeUsed
                                    / (Environment.ProcessorCount * totalTimeElapsed)
                                    * 100;

                                // Get memory usage in MB
                                var memoryMB = _targetProcess.WorkingSet64 / 1024.0 / 1024.0;

                                // Record metrics
                                _metrics.Add((currentTime, cpuUsagePercent, memoryMB));

                                // Reset for next calculation
                                startTime = currentTime;
                                startCpuTime = currentCpuTime;

                                _logger.LogDebug(
                                    "Process {ProcessName} metrics: CPU {CpuPercent:F2}%, Memory {MemoryMB:F2} MB",
                                    _processName,
                                    cpuUsagePercent,
                                    memoryMB
                                );

                                // Wait for next sample
                                await Task.Delay(1000, _cts.Token);
                            }
                            catch (OperationCanceledException)
                            {
                                break;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error monitoring process metrics");
                                await Task.Delay(1000, _cts.Token);
                            }
                        }

                        _logger.LogInformation(
                            "Process monitoring stopped for {ProcessName}",
                            _processName
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Process monitoring task failed");
                    }
                },
                _cts.Token
            );

            return Task.CompletedTask;
        }

        /// <summary>
        /// Stop monitoring and return metrics
        /// </summary>
        public async Task<(double AvgCpuPercent, double PeakMemoryMB)> StopMonitoringAsync()
        {
            if (!_isMonitoring)
            {
                return (0, 0);
            }

            _isMonitoring = false;

            try
            {
                // Cancel the monitoring task
                if (_cts != null)
                {
                    _cts.Cancel();
                }

                // Wait for the task to complete
                if (_monitoringTask != null)
                {
                    await _monitoringTask;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping process monitoring");
            }

            // Calculate final metrics
            double avgCpuPercent = 0;
            double peakMemoryMB = 0;

            if (_metrics.Count > 0)
            {
                avgCpuPercent = _metrics.Average(m => m.CpuPercent);
                peakMemoryMB = _metrics.Max(m => m.MemoryMB);
            }

            _logger.LogInformation(
                "Process monitoring results for {ProcessName}: Avg CPU {AvgCpu:F2}%, Peak Memory {PeakMemory:F2} MB",
                _processName,
                avgCpuPercent,
                peakMemoryMB
            );

            return (avgCpuPercent, peakMemoryMB);
        }

        /// <summary>
        /// Clean up resources
        /// </summary>
        public Task CloseAsync()
        {
            if (_isMonitoring)
            {
                _ = StopMonitoringAsync();
            }

            _cts?.Dispose();
            _targetProcess?.Dispose();
            _targetProcess = null;

            return Task.CompletedTask;
        }
    }
}
