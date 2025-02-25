// File: Pulsar.Compiler/Config/Templates/RuntimeConfig.cs
// Version: 1.0.0

using System;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Beacon.Runtime.Services;
using Serilog.Events;
using YamlDotNet.Serialization;

namespace Beacon.Runtime.Rules
{
    public class RuntimeConfig
    {
        [JsonPropertyName("redis")]
        [YamlMember(Alias = "redis")]
        public RedisConfiguration Redis { get; set; } = new();

        [JsonPropertyName("cycleTime")]
        [YamlMember(Alias = "cycleTime")]
        [JsonConverter(typeof(TimeSpanConverter))]
        public TimeSpan? CycleTime { get; set; }

        [JsonPropertyName("logLevel")]
        [YamlMember(Alias = "logLevel")]
        public LogEventLevel LogLevel { get; set; } = LogEventLevel.Information;

        [JsonPropertyName("bufferCapacity")]
        [YamlMember(Alias = "bufferCapacity")]
        public int BufferCapacity { get; set; } = 100;

        [JsonPropertyName("logFile")]
        [YamlMember(Alias = "logFile")]
        public string? LogFile { get; set; }

        [JsonPropertyName("validSensors")]
        [YamlMember(Alias = "validSensors")]
        public string[] RequiredSensors { get; set; } = Array.Empty<string>();
    }
}
