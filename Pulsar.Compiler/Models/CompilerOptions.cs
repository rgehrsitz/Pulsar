// File: Pulsar.Compiler/Models/CompilerOptions.cs

using Pulsar.Compiler.Config;
using Serilog;

namespace Pulsar.Compiler.Models
{
    public class CompilerOptions
    {
        private static readonly ILogger _logger = LoggingConfig.GetLogger();

        public BuildConfig BuildConfig { get; set; } =
            new BuildConfig
            {
                OutputPath = "build",
                Target = "library",
                ProjectName = "PulsarRules",
                TargetFramework = "net9.0",
                RulesPath = "rules",
            };
        public List<string> ValidSensors { get; set; } = new();
        public bool StrictMode { get; set; }
        public bool AllowInvalidSensors { get; set; }
        public string OutputDirectory { get; set; } = "build";
        public string TargetFramework { get; set; } = "net9.0";
        public string RuntimeIdentifier { get; set; } = "win-x64";

        public void Validate()
        {
            try
            {
                _logger.Debug("Validating compiler options");

                if (BuildConfig == null)
                {
                    _logger.Error("BuildConfig is required but was null");
                    throw new ArgumentNullException(nameof(BuildConfig));
                }

                if (string.IsNullOrEmpty(BuildConfig.OutputPath))
                {
                    _logger.Error("BuildConfig.OutputPath is required but was empty");
                    throw new ArgumentException(
                        "OutputPath is required",
                        nameof(BuildConfig.OutputPath)
                    );
                }

                _logger.Debug("Compiler options validation successful");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Compiler options validation failed");
                throw;
            }
        }

        public void LogOptions()
        {
            _logger.Information(
                "Compiler options: OutputPath={OutputPath}, StrictMode={StrictMode}, ValidSensors={SensorCount}",
                BuildConfig.OutputPath,
                StrictMode,
                ValidSensors.Count
            );
        }
    }
}
