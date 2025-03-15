using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Beacon.PerformanceTester.Common;

namespace Beacon.PerformanceTester.Visualization.Services
{
    /// <summary>
    /// Interface for plot generation services
    /// </summary>
    public interface IPlotService
    {
        /// <summary>
        /// Generate performance plots for a test case result
        /// </summary>
        /// <param name="result">The test case result</param>
        /// <param name="outputPath">Directory to save plots to</param>
        /// <returns>List of generated file paths</returns>
        Task<List<string>> GeneratePlotsForTestCaseAsync(TestCaseResult result, string outputPath);

        /// <summary>
        /// Generate a summary plot for a test scenario result
        /// </summary>
        /// <param name="result">The test scenario result</param>
        /// <param name="outputPath">Directory to save plot to</param>
        /// <returns>Path to the generated summary plot</returns>
        Task<string> GenerateScenarioSummaryPlotAsync(TestScenarioResult result, string outputPath);

        /// <summary>
        /// Generate a latency distribution plot
        /// </summary>
        /// <param name="result">The test case result</param>
        /// <param name="outputPath">Directory to save plot to</param>
        /// <returns>Path to the generated plot</returns>
        Task<string> GenerateLatencyDistributionPlotAsync(TestCaseResult result, string outputPath);

        /// <summary>
        /// Generate a time series plot for a specific metric
        /// </summary>
        /// <param name="result">The test case result</param>
        /// <param name="metricName">Name of the metric to plot</param>
        /// <param name="outputPath">Directory to save plot to</param>
        /// <returns>Path to the generated plot</returns>
        Task<string> GenerateTimeSeriesPlotAsync(
            TestCaseResult result,
            string metricName,
            string outputPath
        );

        /// <summary>
        /// Generate an HTML report for a test scenario
        /// </summary>
        /// <param name="result">The test scenario result</param>
        /// <param name="outputPath">Directory to save report to</param>
        /// <returns>Path to the generated HTML report</returns>
        Task<string> GenerateHtmlReportAsync(TestScenarioResult result, string outputPath);
    }
}
