// File: Pulsar.Compiler/Config/Templates/RuntimeConfig.cs
// Version: 1.0.0

using System;
using Serilog.Events;
using System.Text.Json;
using System.ComponentModel;
using System.Text.Json.Serialization;
using Pulsar.Runtime.Services;

namespace Pulsar.Runtime.Rules
{
    public class RuntimeConfig
    {
        [JsonPropertyName("Redis")]
        public RedisConfiguration Redis { get; set; } = new();

        [JsonPropertyName("CycleTime")]
        [JsonConverter(typeof(TimeSpanConverter))]
        public TimeSpan? CycleTime { get; set; }

        [JsonPropertyName("LogLevel")]
        public LogEventLevel LogLevel { get; set; } = LogEventLevel.Information;

        [JsonPropertyName("BufferCapacity")]
        public int BufferCapacity { get; set; } = 100;

        [JsonPropertyName("LogFile")]
        public string? LogFile { get; set; }

        [JsonPropertyName("RequiredSensors")]
        public string[] RequiredSensors { get; set; } = Array.Empty<string>();
    }
}
