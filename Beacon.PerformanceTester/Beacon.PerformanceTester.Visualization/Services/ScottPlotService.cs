using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Beacon.PerformanceTester.Common;
using Microsoft.Extensions.Logging;
using ScottPlot;
using ScottPlot.Plottables;

namespace Beacon.PerformanceTester.Visualization.Services
{
    /// <summary>
    /// Implementation of plot service using ScottPlot 5.0
    /// </summary>
    public class ScottPlotService : IPlotService
    {
        private readonly ILogger<ScottPlotService> _logger;

        public ScottPlotService(ILogger<ScottPlotService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Generate all plots for a test case
        /// </summary>
        public async Task<List<string>> GeneratePlotsForTestCaseAsync(
            TestCaseResult result,
            string outputPath
        )
        {
            _logger.LogInformation(
                "Generating plots for test case: {TestCase}",
                result.TestCaseName
            );

            // Ensure the output directory exists
            Directory.CreateDirectory(outputPath);

            var generatedFiles = new List<string>();

            try
            {
                // Generate latency plot
                var latencyPlotPath = await GenerateLatencyDistributionPlotAsync(
                    result,
                    outputPath
                );
                generatedFiles.Add(latencyPlotPath);

                // Generate time series plots if data exists
                if (result.TimeSeriesData != null && result.TimeSeriesData.Count > 0)
                {
                    var latencyTimeSeriesPath = await GenerateTimeSeriesPlotAsync(
                        result,
                        "Latency",
                        outputPath
                    );
                    generatedFiles.Add(latencyTimeSeriesPath);

                    var cpuTimeSeriesPath = await GenerateTimeSeriesPlotAsync(
                        result,
                        "CPU",
                        outputPath
                    );
                    generatedFiles.Add(cpuTimeSeriesPath);

                    var memoryTimeSeriesPath = await GenerateTimeSeriesPlotAsync(
                        result,
                        "Memory",
                        outputPath
                    );
                    generatedFiles.Add(memoryTimeSeriesPath);

                    // Generate combined metrics plot
                    var combinedPlotPath = await GenerateCombinedMetricsPlotAsync(
                        result,
                        outputPath
                    );
                    generatedFiles.Add(combinedPlotPath);
                }

                _logger.LogInformation(
                    "Generated {Count} plots for test case: {TestCase}",
                    generatedFiles.Count,
                    result.TestCaseName
                );

                return generatedFiles;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error generating plots for test case: {TestCase}",
                    result.TestCaseName
                );
                return generatedFiles;
            }
        }

        /// <summary>
        /// Generate a summary plot for a test scenario
        /// </summary>
        public async Task<string> GenerateScenarioSummaryPlotAsync(
            TestScenarioResult result,
            string outputPath
        )
        {
            _logger.LogInformation(
                "Generating summary plot for scenario: {Scenario}",
                result.ScenarioName
            );

            Directory.CreateDirectory(outputPath);
            string filePath = Path.Combine(
                outputPath,
                $"scenario_summary_{result.ScenarioName.Replace(" ", "_")}.png"
            );

            try
            {
                // Create a new plot
                var plot = new Plot();

                // If we have multiple test cases, show comparison
                if (result.TestCaseResults.Count > 1)
                {
                    // Extract test case names and latencies for bar chart
                    string[] labels = result.TestCaseResults.Select(r => r.TestCaseName).ToArray();
                    double[] avgLatencies = result
                        .TestCaseResults.Select(r => r.AverageLatencyMs)
                        .ToArray();
                    double[] p95Latencies = result
                        .TestCaseResults.Select(r => r.P95LatencyMs)
                        .ToArray();
                    double[] maxLatencies = result
                        .TestCaseResults.Select(r => r.MaxLatencyMs)
                        .ToArray();

                    // Create multi-bar chart
                    int groupCount = labels.Length;
                    int barCount = 3; // avg, p95, max

                    // Create bar data
                    var barData = new ScottPlot.MultiBar(groupCount, barCount);
                    for (int i = 0; i < groupCount; i++)
                    {
                        barData.SetBarValues(
                            i,
                            new double[] { avgLatencies[i], p95Latencies[i], maxLatencies[i] }
                        );
                    }

                    // Add bar plot
                    var barPlot = plot.Add.Bars(barData);

                    // Set up colors
                    barPlot.FillColors = new Color[]
                    {
                        new Color(0, 100, 200), // blue
                        new Color(200, 100, 0), // orange
                        new Color(
                            200,
                            50,
                            50
                        ) // red
                        ,
                    };

                    // Set up labels
                    plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(
                        Enumerable.Range(0, groupCount).Select(i => (double)i).ToArray(),
                        labels
                    );

                    // Add legend items manually
                    var legend = plot.Add.Legend();
                    legend.AddItem("Avg Latency (ms)", barPlot.FillColors[0]);
                    legend.AddItem("P95 Latency (ms)", barPlot.FillColors[1]);
                    legend.AddItem("Max Latency (ms)", barPlot.FillColors[2]);

                    // Add labels and styling
                    plot.Title($"Scenario: {result.ScenarioName} - Latency Comparison");
                    plot.Axes.Bottom.Label = "Test Case";
                    plot.Axes.Left.Label = "Latency (ms)";

                    // Save the plot
                    plot.SavePng(filePath, 1200, 800);
                }
                else if (result.TestCaseResults.Count == 1)
                {
                    // For a single test case, just show its metrics
                    var testCase = result.TestCaseResults[0];

                    // Create data for single bar chart
                    string[] labels = new[] { "Avg Latency", "P95 Latency", "Max Latency" };
                    double[] values = new[]
                    {
                        testCase.AverageLatencyMs,
                        testCase.P95LatencyMs,
                        testCase.MaxLatencyMs,
                    };

                    // Create bar plot
                    var barPlot = plot.Add.Bars(values);
                    barPlot.FillColor = new Color(0, 100, 200); // blue

                    // Set up labels
                    plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(
                        Enumerable.Range(0, labels.Length).Select(i => (double)i).ToArray(),
                        labels
                    );

                    // Add labels and styling
                    plot.Title($"Test Case: {testCase.TestCaseName} - Latency Metrics");
                    plot.Axes.Left.Label = "Milliseconds";

                    // Save the plot
                    plot.SavePng(filePath, 800, 600);
                }

                _logger.LogInformation("Generated scenario summary plot: {FilePath}", filePath);
                return filePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error generating scenario summary plot for {Scenario}",
                    result.ScenarioName
                );
                return string.Empty;
            }
        }

        /// <summary>
        /// Generate a latency distribution plot
        /// </summary>
        public async Task<string> GenerateLatencyDistributionPlotAsync(
            TestCaseResult result,
            string outputPath
        )
        {
            _logger.LogInformation(
                "Generating latency distribution plot for test case: {TestCase}",
                result.TestCaseName
            );

            Directory.CreateDirectory(outputPath);
            string filePath = Path.Combine(
                outputPath,
                $"latency_distribution_{result.TestCaseName.Replace(" ", "_")}.png"
            );

            try
            {
                // Create a new plot
                var plot = new Plot();

                // Extract latencies from results
                var latencies = result
                    .OutputResults.Where(o => o.LatencyMs > 0)
                    .Select(o => o.LatencyMs)
                    .ToArray();

                if (latencies.Length > 0)
                {
                    // Create histogram
                    var hist = plot.Add.Histogram(latencies);
                    hist.FillColor = new Color(0, 100, 200, 128); // blue with transparency
                    hist.BorderColor = new Color(0, 100, 200); // blue border
                    hist.BorderLineWidth = 1;

                    // Add vertical markers for key metrics
                    var avgLine = plot.Add.VerticalLine(result.AverageLatencyMs);
                    avgLine.Color = new Color(200, 50, 50); // red
                    avgLine.LineWidth = 2;
                    avgLine.LinePattern = LinePattern.Dashed;

                    var p95Line = plot.Add.VerticalLine(result.P95LatencyMs);
                    p95Line.Color = new Color(200, 100, 0); // orange
                    p95Line.LineWidth = 2;
                    p95Line.LinePattern = LinePattern.Dashed;

                    // Add legend
                    var legend = plot.Add.Legend();
                    legend.AddItem($"Avg: {result.AverageLatencyMs:F1} ms", avgLine.Color);
                    legend.AddItem($"P95: {result.P95LatencyMs:F1} ms", p95Line.Color);

                    // Add labels and styling
                    plot.Title($"Latency Distribution: {result.TestCaseName}");
                    plot.Axes.Bottom.Label = "Latency (ms)";
                    plot.Axes.Left.Label = "Frequency";
                }
                else
                {
                    // No valid latency data, show message
                    plot.Title($"No Valid Latency Data for {result.TestCaseName}");
                    plot.Add.Text(
                        "No latency data available",
                        0.5,
                        0.5,
                        HorizontalAlignment.Center,
                        VerticalAlignment.Middle
                    );
                }

                // Save the plot
                plot.SavePng(filePath, 800, 600);

                _logger.LogInformation("Generated latency distribution plot: {FilePath}", filePath);
                return filePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error generating latency distribution plot for {TestCase}",
                    result.TestCaseName
                );
                return string.Empty;
            }
        }

        /// <summary>
        /// Generate a time series plot for a specific metric
        /// </summary>
        public async Task<string> GenerateTimeSeriesPlotAsync(
            TestCaseResult result,
            string metricName,
            string outputPath
        )
        {
            _logger.LogInformation(
                "Generating {Metric} time series plot for test case: {TestCase}",
                metricName,
                result.TestCaseName
            );

            Directory.CreateDirectory(outputPath);
            string filePath = Path.Combine(
                outputPath,
                $"{metricName.ToLower()}_timeseries_{result.TestCaseName.Replace(" ", "_")}.png"
            );

            try
            {
                // Create a new plot
                var plot = new Plot();

                // Check if we have time series data
                if (result.TimeSeriesData == null || result.TimeSeriesData.Count == 0)
                {
                    plot.Title($"No Time Series Data Available for {result.TestCaseName}");
                    plot.Add.Text(
                        "No time series data available",
                        0.5,
                        0.5,
                        HorizontalAlignment.Center,
                        VerticalAlignment.Middle
                    );

                    plot.SavePng(filePath, 800, 600);
                    return filePath;
                }

                // Extract X values (time)
                double[] times = result
                    .TimeSeriesData.Select(p => p.TimeMs / 1000.0) // Convert to seconds
                    .ToArray();

                // Extract Y values based on metric name
                double[] values;
                string yAxisLabel;

                switch (metricName.ToLower())
                {
                    case "latency":
                        values = result.TimeSeriesData.Select(p => p.LatencyMs).ToArray();
                        yAxisLabel = "Latency (ms)";
                        break;
                    case "cpu":
                        values = result.TimeSeriesData.Select(p => p.CpuPercent).ToArray();
                        yAxisLabel = "CPU Usage (%)";
                        break;
                    case "memory":
                        values = result.TimeSeriesData.Select(p => p.MemoryMB).ToArray();
                        yAxisLabel = "Memory Usage (MB)";
                        break;
                    case "inputrate":
                        values = result.TimeSeriesData.Select(p => p.InputRatePerSec).ToArray();
                        yAxisLabel = "Input Rate (ops/sec)";
                        break;
                    case "outputrate":
                        values = result.TimeSeriesData.Select(p => p.OutputRatePerSec).ToArray();
                        yAxisLabel = "Output Rate (ops/sec)";
                        break;
                    default:
                        values = result.TimeSeriesData.Select(p => p.LatencyMs).ToArray();
                        yAxisLabel = "Value";
                        break;
                }

                // Add the line plot
                var linePlot = plot.Add.Scatter(times, values);
                linePlot.LineWidth = 2;

                // Add labels and styling
                plot.Title($"{metricName} Over Time: {result.TestCaseName}");
                plot.Axes.Bottom.Label = "Time (seconds)";
                plot.Axes.Left.Label = yAxisLabel;

                // Add average line if relevant
                if (metricName.ToLower() == "latency")
                {
                    double avgLatency = result.AverageLatencyMs;
                    var avgLine = plot.Add.HorizontalLine(avgLatency);
                    avgLine.Color = new Color(200, 50, 50); // red
                    avgLine.LineWidth = 2;
                    avgLine.LinePattern = LinePattern.Dashed;

                    // Add P95 line
                    double p95Latency = result.P95LatencyMs;
                    var p95Line = plot.Add.HorizontalLine(p95Latency);
                    p95Line.Color = new Color(200, 100, 0); // orange
                    p95Line.LineWidth = 2;
                    p95Line.LinePattern = LinePattern.Dashed;

                    // Add legend
                    var legend = plot.Add.Legend();
                    legend.AddItem($"Avg: {avgLatency:F1} ms", avgLine.Color);
                    legend.AddItem($"P95: {p95Latency:F1} ms", p95Line.Color);
                }

                // Save the plot
                plot.SavePng(filePath, 1000, 600);

                _logger.LogInformation(
                    "Generated {Metric} time series plot: {FilePath}",
                    metricName,
                    filePath
                );
                return filePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error generating {Metric} time series plot for {TestCase}",
                    metricName,
                    result.TestCaseName
                );
                return string.Empty;
            }
        }

        /// <summary>
        /// Generate a combined metrics plot
        /// </summary>
        private async Task<string> GenerateCombinedMetricsPlotAsync(
            TestCaseResult result,
            string outputPath
        )
        {
            _logger.LogInformation(
                "Generating combined metrics plot for test case: {TestCase}",
                result.TestCaseName
            );

            Directory.CreateDirectory(outputPath);
            string filePath = Path.Combine(
                outputPath,
                $"combined_metrics_{result.TestCaseName.Replace(" ", "_")}.png"
            );

            try
            {
                // Check if we have time series data
                if (result.TimeSeriesData == null || result.TimeSeriesData.Count == 0)
                {
                    var plot = new Plot();
                    plot.Title($"No Time Series Data Available for {result.TestCaseName}");
                    plot.Add.Text(
                        "No time series data available",
                        0.5,
                        0.5,
                        HorizontalAlignment.Center,
                        VerticalAlignment.Middle
                    );
                    plot.SavePng(filePath, 1000, 800);
                    return filePath;
                }

                // Extract common X values (time)
                double[] times = result
                    .TimeSeriesData.Select(p => p.TimeMs / 1000.0) // Convert to seconds
                    .ToArray();

                // Generate a figure with subplots
                ScottPlot.Figure fig = new();
                fig.Title($"Performance Metrics: {result.TestCaseName}");

                // Create a 2x2 subplot layout
                var subplots = fig.Subplots(2, 2);

                // Plot 1: Latency
                double[] latencies = result.TimeSeriesData.Select(p => p.LatencyMs).ToArray();
                subplots[0, 0].Title("Latency (ms)");
                subplots[0, 0].XLabel("Time (seconds)");
                subplots[0, 0].Add.Scatter(times, latencies);

                // Plot 2: CPU Usage
                double[] cpuValues = result.TimeSeriesData.Select(p => p.CpuPercent).ToArray();
                subplots[0, 1].Title("CPU Usage (%)");
                subplots[0, 1].XLabel("Time (seconds)");
                var cpuScatter = subplots[0, 1].Add.Scatter(times, cpuValues);
                cpuScatter.Color = new Color(200, 50, 50); // red

                // Plot 3: Memory Usage
                double[] memValues = result.TimeSeriesData.Select(p => p.MemoryMB).ToArray();
                subplots[1, 0].Title("Memory Usage (MB)");
                subplots[1, 0].XLabel("Time (seconds)");
                var memScatter = subplots[1, 0].Add.Scatter(times, memValues);
                memScatter.Color = new Color(50, 150, 50); // green

                // Plot 4: Input/Output Rate
                double[] inputRates = result
                    .TimeSeriesData.Select(p => p.InputRatePerSec)
                    .ToArray();
                double[] outputRates = result
                    .TimeSeriesData.Select(p => p.OutputRatePerSec)
                    .ToArray();

                subplots[1, 1].Title("Rate (ops/sec)");
                subplots[1, 1].XLabel("Time (seconds)");

                var inputScatter = subplots[1, 1].Add.Scatter(times, inputRates);
                inputScatter.Color = new Color(0, 100, 200); // blue

                if (outputRates.Any(r => r > 0)) // Only add if we have output rate data
                {
                    var outputScatter = subplots[1, 1].Add.Scatter(times, outputRates);
                    outputScatter.Color = new Color(150, 50, 150); // purple

                    // Add legend
                    var legend = subplots[1, 1].Add.Legend();
                    legend.AddItem("Input Rate", inputScatter.Color);
                    legend.AddItem("Output Rate", outputScatter.Color);
                }

                // Save the figure
                fig.SavePng(filePath, 1200, 1000);

                _logger.LogInformation("Generated combined metrics plot: {FilePath}", filePath);
                return filePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error generating combined metrics plot for {TestCase}",
                    result.TestCaseName
                );
                return string.Empty;
            }
        }

        /// <summary>
        /// Generate an HTML report for a test scenario
        /// </summary>
        public async Task<string> GenerateHtmlReportAsync(
            TestScenarioResult result,
            string outputPath
        )
        {
            _logger.LogInformation(
                "Generating HTML report for scenario: {Scenario}",
                result.ScenarioName
            );

            Directory.CreateDirectory(outputPath);
            string filePath = Path.Combine(
                outputPath,
                $"report_{result.ScenarioName.Replace(" ", "_")}.html"
            );

            try
            {
                // Generate plots for each test case
                var plotPaths = new List<string>();

                foreach (var testCaseResult in result.TestCaseResults)
                {
                    var casePlots = await GeneratePlotsForTestCaseAsync(testCaseResult, outputPath);
                    plotPaths.AddRange(casePlots);
                }

                // Generate scenario summary plot
                var summaryPlotPath = await GenerateScenarioSummaryPlotAsync(result, outputPath);
                plotPaths.Add(summaryPlotPath);

                // Make all paths relative to the output directory
                var relativePlotPaths = plotPaths
                    .Where(p => !string.IsNullOrEmpty(p))
                    .Select(p => Path.GetFileName(p))
                    .ToList();

                // Build HTML report
                var html = new StringBuilder();

                // Add header
                html.AppendLine("<!DOCTYPE html>");
                html.AppendLine("<html lang=\"en\">");
                html.AppendLine("<head>");
                html.AppendLine("  <meta charset=\"UTF-8\">");
                html.AppendLine(
                    "  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">"
                );
                html.AppendLine($"  <title>Performance Test Report: {result.ScenarioName}</title>");
                html.AppendLine("  <style>");
                html.AppendLine(
                    "    body { font-family: Arial, sans-serif; margin: 20px; line-height: 1.6; }"
                );
                html.AppendLine("    h1, h2, h3 { color: #333; }");
                html.AppendLine(
                    "    .summary { background-color: #f5f5f5; padding: 15px; border-radius: 5px; margin-bottom: 20px; }"
                );
                html.AppendLine("    .success { color: green; }");
                html.AppendLine("    .failure { color: red; }");
                html.AppendLine(
                    "    table { border-collapse: collapse; width: 100%; margin-bottom: 20px; }"
                );
                html.AppendLine(
                    "    th, td { padding: 12px; text-align: left; border-bottom: 1px solid #ddd; }"
                );
                html.AppendLine("    th { background-color: #f2f2f2; }");
                html.AppendLine("    tr:hover { background-color: #f5f5f5; }");
                html.AppendLine("    .plot-container { margin: 20px 0; text-align: center; }");
                html.AppendLine("    img { max-width: 100%; border: 1px solid #ddd; }");
                html.AppendLine(
                    "    .test-case { margin-bottom: 30px; border: 1px solid #ddd; padding: 15px; border-radius: 5px; }"
                );
                html.AppendLine("  </style>");
                html.AppendLine("</head>");
                html.AppendLine("<body>");

                // Add report header
                html.AppendLine($"  <h1>Performance Test Report: {result.ScenarioName}</h1>");

                // Add summary section
                html.AppendLine("  <div class=\"summary\">");
                html.AppendLine($"    <h2>Test Summary</h2>");
                html.AppendLine(
                    $"    <p><strong>Status:</strong> <span class=\"{(result.Success ? "success" : "failure")}\">{(result.Success ? "SUCCESS" : "FAILURE")}</span></p>"
                );
                html.AppendLine($"    <p><strong>Start Time:</strong> {result.StartTime}</p>");
                html.AppendLine($"    <p><strong>End Time:</strong> {result.EndTime}</p>");
                html.AppendLine(
                    $"    <p><strong>Duration:</strong> {result.TotalDurationMs / 1000:F2} seconds</p>"
                );
                html.AppendLine(
                    $"    <p><strong>Test Cases:</strong> {result.TestCaseResults.Count}</p>"
                );
                html.AppendLine($"    <p><strong>Run ID:</strong> {result.TestRunId}</p>");
                html.AppendLine("  </div>");

                // Add summary plot if available
                if (!string.IsNullOrEmpty(summaryPlotPath))
                {
                    html.AppendLine("  <div class=\"plot-container\">");
                    html.AppendLine($"    <h2>Performance Summary</h2>");
                    html.AppendLine(
                        $"    <img src=\"{Path.GetFileName(summaryPlotPath)}\" alt=\"Scenario Summary Plot\">"
                    );
                    html.AppendLine("  </div>");
                }

                // Add test case results
                html.AppendLine("  <h2>Test Case Results</h2>");

                foreach (var testCaseResult in result.TestCaseResults)
                {
                    html.AppendLine($"  <div class=\"test-case\">");
                    html.AppendLine($"    <h3>{testCaseResult.TestCaseName}</h3>");
                    html.AppendLine(
                        $"    <p><strong>Status:</strong> <span class=\"{(testCaseResult.Success ? "success" : "failure")}\">{(testCaseResult.Success ? "PASS" : "FAIL")}</span></p>"
                    );
                    html.AppendLine(
                        $"    <p><strong>Duration:</strong> {testCaseResult.DurationMs / 1000:F2} seconds</p>"
                    );

                    // Add metrics table
                    html.AppendLine("    <table>");
                    html.AppendLine("      <tr><th>Metric</th><th>Value</th></tr>");
                    html.AppendLine(
                        $"      <tr><td>Average Latency</td><td>{testCaseResult.AverageLatencyMs:F2} ms</td></tr>"
                    );
                    html.AppendLine(
                        $"      <tr><td>P95 Latency</td><td>{testCaseResult.P95LatencyMs:F2} ms</td></tr>"
                    );
                    html.AppendLine(
                        $"      <tr><td>Maximum Latency</td><td>{testCaseResult.MaxLatencyMs:F2} ms</td></tr>"
                    );
                    html.AppendLine(
                        $"      <tr><td>CPU Usage (avg)</td><td>{testCaseResult.AvgCpuPercent:F2}%</td></tr>"
                    );
                    html.AppendLine(
                        $"      <tr><td>Memory Usage (peak)</td><td>{testCaseResult.PeakMemoryMB:F2} MB</td></tr>"
                    );
                    html.AppendLine(
                        $"      <tr><td>Input Rate</td><td>{testCaseResult.InputRatePerSecond:F2} ops/sec</td></tr>"
                    );
                    html.AppendLine(
                        $"      <tr><td>Error Rate</td><td>{testCaseResult.ErrorRatePercent:F2}%</td></tr>"
                    );
                    html.AppendLine("    </table>");

                    // Add plots for this test case
                    var testCasePlots = relativePlotPaths
                        .Where(p => p.Contains(testCaseResult.TestCaseName.Replace(" ", "_")))
                        .ToList();

                    if (testCasePlots.Any())
                    {
                        html.AppendLine("    <h4>Performance Graphs</h4>");

                        foreach (var plotPath in testCasePlots)
                        {
                            html.AppendLine("    <div class=\"plot-container\">");
                            html.AppendLine(
                                $"      <img src=\"{plotPath}\" alt=\"Performance Plot\">"
                            );
                            html.AppendLine("    </div>");
                        }
                    }

                    // Add output details if any failures
                    if (
                        !testCaseResult.Success && testCaseResult.OutputResults.Any(o => !o.IsMatch)
                    )
                    {
                        html.AppendLine("    <h4>Failed Outputs</h4>");
                        html.AppendLine("    <table>");
                        html.AppendLine(
                            "      <tr><th>Output Key</th><th>Expected</th><th>Actual</th><th>Latency</th></tr>"
                        );

                        foreach (var output in testCaseResult.OutputResults.Where(o => !o.IsMatch))
                        {
                            html.AppendLine($"      <tr>");
                            html.AppendLine($"        <td>{output.Key}</td>");
                            html.AppendLine($"        <td>{output.ExpectedValue}</td>");
                            html.AppendLine($"        <td>{output.ActualValue}</td>");
                            html.AppendLine($"        <td>{output.LatencyMs:F2} ms</td>");
                            html.AppendLine($"      </tr>");
                        }

                        html.AppendLine("    </table>");
                    }

                    html.AppendLine("  </div>");
                }

                // Add footer
                html.AppendLine("  <footer>");
                html.AppendLine($"    <p>Report generated at {DateTime.Now}</p>");
                html.AppendLine("    <p>Beacon Performance Tester</p>");
                html.AppendLine("  </footer>");

                html.AppendLine("</body>");
                html.AppendLine("</html>");

                // Write the HTML to file
                await File.WriteAllTextAsync(filePath, html.ToString());

                _logger.LogInformation("Generated HTML report: {FilePath}", filePath);
                return filePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error generating HTML report for {Scenario}",
                    result.ScenarioName
                );
                return string.Empty;
            }
        }
    }
}
