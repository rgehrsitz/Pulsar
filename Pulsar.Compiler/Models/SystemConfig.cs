// File: Pulsar.Compiler/Models/SystemConfig.cs

using System.Text.Json;
// Template-based approach
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
        public Dictionary<string, object> Redis { get; set; } = new();

        [YamlMember(Alias = "bufferCapacity")]
        public int BufferCapacity { get; set; } = 100;

        [YamlMember(Alias = "logLevel")]
        public string LogLevel { get; set; } = "Information";

        [YamlMember(Alias = "logFile")]
        public string LogFile { get; set; } = "logs/pulsar.log";

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
                _logger.Debug("YAML content: {Content}", yaml);

                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(
                        YamlDotNet
                            .Serialization
                            .NamingConventions
                            .CamelCaseNamingConvention
                            .Instance
                    )
                    .IgnoreUnmatchedProperties()
                    .Build();
                var config = deserializer.Deserialize<SystemConfig>(yaml);

                // Initialize ValidSensors if null to avoid null reference
                config.ValidSensors ??= new List<string>();

                // Manually parse validSensors if they weren't deserialized properly
                if (config.ValidSensors.Count == 0) // Fix: Null check removed as we ensure it's not null above
                {
                    _logger.Warning(
                        "ValidSensors not properly deserialized, attempting manual parsing"
                    );
                    try
                    {
                        // Parse the YAML manually to extract validSensors
                        var lines = yaml.Split('\n');
                        bool inValidSensors = false;
                        foreach (var line in lines)
                        {
                            var trimmedLine = line.Trim();
                            if (trimmedLine.StartsWith("validSensors:"))
                            {
                                inValidSensors = true;
                                // No need to reinitialize ValidSensors as we already ensured it's not null
                                continue;
                            }

                            if (inValidSensors && trimmedLine.StartsWith("-"))
                            {
                                var sensor = trimmedLine.Substring(1).Trim();
                                config.ValidSensors.Add(sensor);
                                _logger.Debug("Manually added sensor: {Sensor}", sensor);
                            }
                            else if (
                                inValidSensors
                                && !string.IsNullOrWhiteSpace(trimmedLine)
                                && !trimmedLine.StartsWith("#")
                                && !trimmedLine.StartsWith("-")
                            )
                            {
                                inValidSensors = false;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Error during manual parsing of validSensors");
                    }
                }

                // Ensure ValidSensors is not null before accessing .Count
                var sensorCount = config.ValidSensors?.Count ?? 0;
                var sensorsString =
                    config.ValidSensors != null
                        ? string.Join(", ", config.ValidSensors)
                        : string.Empty;

                _logger.Information(
                    "Successfully loaded system configuration with {SensorCount} valid sensors: {Sensors}",
                    sensorCount,
                    sensorsString
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
