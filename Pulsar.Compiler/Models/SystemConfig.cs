// File: Pulsar.Compiler/Models/SystemConfig.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Pulsar.Compiler;
using Pulsar.Runtime.Services;
using Serilog;
using YamlDotNet.Serialization;

namespace Pulsar.Compiler.Models
{
    public class SystemConfig
    {
        private static readonly ILogger _logger = LoggingConfig.GetLogger();

        [YamlMember(Alias = "version")]
        public int Version { get; set; }

        [YamlMember(Alias = "validSensors")] // Updated alias to match YAML key
        public List<string> ValidSensors { get; set; } = new();

        [YamlMember(Alias = "cycleTime")]
        public int CycleTime { get; set; } = 100; // Default 100ms

        [YamlMember(Alias = "redis")]
        public RedisConfiguration Redis { get; set; } = new();

        [YamlMember(Alias = "bufferCapacity")]
        public int BufferCapacity { get; set; } = 100;

        public static SystemConfig Load(string path)
        {
            try
            {
                _logger.Debug("Loading system configuration from {Path}", path);

                if (!File.Exists(path))
                {
                    _logger.Warning("Configuration file not found at {Path}, using defaults", path);
                    return new SystemConfig();
                }

                var yaml = File.ReadAllText(path);
                var deserializer = new DeserializerBuilder().Build();
                var config = deserializer.Deserialize<SystemConfig>(yaml);

                _logger.Information(
                    "Successfully loaded system configuration with {SensorCount} valid sensors",
                    config.ValidSensors.Count
                );
                return config;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load system configuration from {Path}", path);
                throw;
            }
        }

        public void Save(string path)
        {
            try
            {
                _logger.Debug("Saving system configuration to {Path}", path);
                var json = JsonSerializer.Serialize(
                    this,
                    new JsonSerializerOptions { WriteIndented = true }
                );
                File.WriteAllText(path, json);
                _logger.Information("Successfully saved system configuration");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to save system configuration to {Path}", path);
                throw;
            }
        }
    }
}
