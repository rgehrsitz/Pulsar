// File: Pulsar.Tests/TestUtilities/RuntimeEngine.cs



using System.Collections.Generic;
using Pulsar.Compiler;
using Serilog;

namespace Pulsar.Tests.TestUtilities
{
    public static class RuntimeEngine
    {
        private static readonly ILogger _logger = LoggingConfig.ToSerilogLogger(
            LoggingConfig.GetLogger()
        );

        public static Dictionary<string, string> RunCycle(
            string[] rules,
            Dictionary<string, string> simulatedSensorInput
        )
        {
            _logger.Debug("Running cycle with {RuleCount} rules", rules.Length);

            _logger.Debug("Simulated sensor inputs: {@Inputs}", simulatedSensorInput);

            // In a real implementation, the compiled rules would process the sensor inputs

            // Here, we simulate runtime execution and return a dummy output

            return new Dictionary<string, string> { { "result", "success" } };
        }

        public static List<string> RunCycleWithLogging(
            string[] rules,
            Dictionary<string, string> simulatedSensorInput
        )
        {
            _logger.Debug("Running cycle with logging. Rules: {RuleCount}", rules.Length);

            var logs = new List<string>();

            logs.Add("Cycle Started");

            logs.Add($"Processing rules: {rules.Length}");

            logs.Add($"Processed Rules: {rules.Length}");

            logs.Add("Cycle Duration: 50ms");

            logs.Add("Cycle Ended");

            _logger.Debug("Cycle completed. Generated {LogCount} log entries", logs.Count);

            return logs;
        }
    }
}
