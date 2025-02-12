// File: Pulsar.Compiler/Config/Templates/ConfigurationLoader.cs

using System;
using System.Text.Json;
using System.IO;
using Serilog;

namespace Pulsar.Runtime.Rules
{
    internal static class ConfigurationLoader
    {
        internal static RuntimeConfig LoadConfiguration(string[] args, bool requireSensors = true, string? configPath = null)
        {
            var config = new RuntimeConfig();

            if (configPath != null && File.Exists(configPath))
            {
                var jsonContent = File.ReadAllText(configPath);
                config = JsonSerializer.Deserialize<RuntimeConfig>(jsonContent) ?? new RuntimeConfig();
            }

            return config;
        }
    }
}
