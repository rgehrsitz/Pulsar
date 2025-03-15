using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Beacon.PerformanceTester.Common;
using Beacon.PerformanceTester.Visualization.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Beacon.PerformanceTester.Visualization
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            // Setup configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .AddCommandLine(args)
                .Build();

            // Setup DI
            var serviceProvider = new ServiceCollection()
                .AddLogging(builder =>
                {
                    builder.AddConfiguration(configuration.GetSection("Logging"));
                    builder.AddConsole();
                })
                .AddSingleton<IConfiguration>(configuration)
                .AddSingleton<IPlotService, ScottPlotService>()
                .BuildServiceProvider();

            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Beacon Performance Tester - Visualization starting");

            try
            {
                // Get command line parameters
                string inputFile = configuration["inputFile"];
                string outputDirectory = configuration["outputDirectory"] ?? "./reports";

                if (string.IsNullOrEmpty(inputFile))
                {
                    logger.LogError("No input file specified. Use --inputFile parameter.");
                    return;
                }

                // Check if input file exists
                if (!File.Exists(inputFile))
                {
                    logger.LogError("Input file not found: {InputFile}", inputFile);
                    return;
                }

                // Create output directory if it doesn't exist
                Directory.CreateDirectory(outputDirectory);

                // Read test results from file
                logger.LogInformation("Reading test results from {InputFile}", inputFile);
                string json = await File.ReadAllTextAsync(inputFile);

                // Try to parse as a test scenario result first, then as a test case result
                TestScenarioResult? scenarioResult = null;
                TestCaseResult? testCaseResult = null;

                try
                {
                    scenarioResult = JsonSerializer.Deserialize<TestScenarioResult>(json);
                    if (scenarioResult != null && string.IsNullOrEmpty(scenarioResult.ScenarioName))
                    {
                        // Might not be a valid scenario result
                        scenarioResult = null;
                    }
                }
                catch (JsonException)
                {
                    // Not a scenario result, try as test case
                    scenarioResult = null;
                }

                if (scenarioResult == null)
                {
                    try
                    {
                        testCaseResult = JsonSerializer.Deserialize<TestCaseResult>(json);
                        if (
                            testCaseResult != null
                            && string.IsNullOrEmpty(testCaseResult.TestCaseName)
                        )
                        {
                            // Might not be a valid test case result
                            testCaseResult = null;
                        }
                    }
                    catch (JsonException)
                    {
                        testCaseResult = null;
                    }
                }

                if (scenarioResult == null && testCaseResult == null)
                {
                    logger.LogError(
                        "Could not parse input file as test scenario or test case result"
                    );
                    return;
                }

                // Get the plot service
                var plotService = serviceProvider.GetRequiredService<IPlotService>();

                if (scenarioResult != null)
                {
                    // Process scenario result
                    logger.LogInformation(
                        "Processing test scenario: {ScenarioName}",
                        scenarioResult.ScenarioName
                    );

                    // Generate HTML report
                    var reportPath = await plotService.GenerateHtmlReportAsync(
                        scenarioResult,
                        outputDirectory
                    );

                    if (!string.IsNullOrEmpty(reportPath))
                    {
                        logger.LogInformation("Generated HTML report: {ReportPath}", reportPath);
                        logger.LogInformation(
                            "You can open this report in a web browser to view results"
                        );
                    }
                }
                else if (testCaseResult != null)
                {
                    // Process single test case result
                    logger.LogInformation(
                        "Processing test case: {TestCaseName}",
                        testCaseResult.TestCaseName
                    );

                    // Generate plots
                    var plotPaths = await plotService.GeneratePlotsForTestCaseAsync(
                        testCaseResult,
                        outputDirectory
                    );

                    if (plotPaths.Count > 0)
                    {
                        logger.LogInformation(
                            "Generated {Count} plots in {OutputDirectory}",
                            plotPaths.Count,
                            outputDirectory
                        );
                    }

                    // Create a simple HTML report
                    var dummyScenario = new TestScenarioResult
                    {
                        ScenarioName = $"Single Test: {testCaseResult.TestCaseName}",
                        TestCaseResults = new List<TestCaseResult> { testCaseResult },
                        StartTime = DateTime.Now.AddSeconds(-testCaseResult.DurationMs / 1000),
                        EndTime = DateTime.Now,
                        TotalDurationMs = testCaseResult.DurationMs,
                        Success = testCaseResult.Success,
                    };

                    var reportPath = await plotService.GenerateHtmlReportAsync(
                        dummyScenario,
                        outputDirectory
                    );

                    if (!string.IsNullOrEmpty(reportPath))
                    {
                        logger.LogInformation("Generated HTML report: {ReportPath}", reportPath);
                        logger.LogInformation(
                            "You can open this report in a web browser to view results"
                        );
                    }
                }

                logger.LogInformation("Visualization completed successfully");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error visualizing performance test results");
            }
        }
    }
}
