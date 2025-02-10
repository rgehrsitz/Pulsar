using Pulsar.Runtime.Rules;
using Pulsar.Runtime.Buffers;
using Serilog;

namespace Pulsar.Runtime;

/// <summary>
/// Generated rule executor class
/// </summary>
public static class RuleExecutor
{
    private static readonly RingBufferManager _bufferManager = new();
    private static readonly ILogger _logger = new LoggerConfiguration()
        .MinimumLevel.Debug()
        .WriteTo.Console()
        .CreateLogger();

    // Rules will be generated as static methods here
    // Example:
    // [GeneratedRule("rule1.yaml")]
    // public static void Rule1(Dictionary<string, double> inputs, Dictionary<string, double> outputs)
    // {
    //     if (_bufferManager.IsAboveThresholdForDuration("Temperature", 30.0, TimeSpan.FromMinutes(5)))
    //     {
    //         outputs["Alarm"] = 1.0;
    //     }
    // }

    public static void ProcessInputs(Dictionary<string, double> inputs, Dictionary<string, double> outputs)
    {
        try
        {
            // Update buffers with current values
            _bufferManager.UpdateBuffers(inputs);

            // Rule evaluation methods will be called here
            // Generated code will look like:
            // Rule1(inputs, outputs);
            // Rule2(inputs, outputs);
            // etc.
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error processing rules");
        }
    }
}
