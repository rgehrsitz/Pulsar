// File: Pulsar.Compiler/Models/CompilationResult.cs

using Pulsar.Compiler.Config;
using Serilog;

namespace Pulsar.Compiler.Models
{
    public class CompilationResult
    {
        private static readonly ILogger _logger = LoggingConfig.GetLogger();

        public bool Success { get; set; }
        public List<string> Errors { get; set; } = new();
        public GeneratedFileInfo[] GeneratedFiles { get; set; } = Array.Empty<GeneratedFileInfo>();
        public string? OutputPath { get; set; }
        public Dictionary<string, RuleMetrics> Metrics { get; set; } = new();
        public RuleManifest? Manifest { get; set; }
        public List<RuleDefinition> Rules { get; set; } = new();

        public void AddError(string error)
        {
            _logger.Error("Compilation error: {Error}", error);
            Errors.Add(error);
            Success = false;
        }

        public void AddMetric(string ruleName, RuleMetrics metric)
        {
            _logger.Debug("Adding metrics for rule {RuleName}: {@Metrics}", ruleName, metric);
            Metrics[ruleName] = metric;
        }

        public void LogSummary()
        {
            if (Success)
            {
                _logger.Information(
                    "Compilation succeeded. Generated {FileCount} files, Output: {Path}",
                    GeneratedFiles.Length,
                    OutputPath
                );
            }
            else
            {
                _logger.Error("Compilation failed with {ErrorCount} errors", Errors.Count);
            }
        }
    }
}
