// File: Pulsar.Compiler/Config/ConfigurationLoader.cs

using Pulsar.Runtime;
using Serilog;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Pulsar.Compiler.Config
{
    internal static class ConfigurationLoader
    {
        private static readonly ILogger _logger = LoggingConfig.GetLogger();

        internal static RuntimeConfig LoadConfiguration(
            string[] args,
            bool requireSensors = true,
            string? configPath = null
        )
        {
            try
            {
                _logger.Debug(
                    "Loading configuration from {Path}",
                    configPath ?? "default location"
                );

                var config = new RuntimeConfig();

                if (configPath != null && File.Exists(configPath))
                {
                    _logger.Debug("Reading configuration file");
                    var yamlContent = File.ReadAllText(configPath);
                    var deserializer = new DeserializerBuilder()
                        .WithNamingConvention(CamelCaseNamingConvention.Instance)
                        .Build();
                    config =
                        deserializer.Deserialize<RuntimeConfig>(yamlContent) ?? new RuntimeConfig();
                    _logger.Information("Configuration loaded from {Path}", configPath);
                }
                else
                {
                    _logger.Warning("No configuration file found, using defaults");
                }

                return config;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error loading configuration");
                throw;
            }
        }
    }
}
