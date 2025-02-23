// File: Pulsar.Compiler/Config/ConfigurationLoader.cs

using System;
using System.Text.Json;
using System.IO;
using Serilog;
using Pulsar.Runtime;
using Pulsar.Runtime.Rules;

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
                _logger.Debug("Loading configuration from {Path}", configPath ?? "default location");

                var config = new RuntimeConfig();

                if (configPath != null && File.Exists(configPath))
                {
                    _logger.Debug("Reading configuration file");
                    var jsonContent = File.ReadAllText(configPath);
                    config = JsonSerializer.Deserialize<RuntimeConfig>(jsonContent) ?? new RuntimeConfig();
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