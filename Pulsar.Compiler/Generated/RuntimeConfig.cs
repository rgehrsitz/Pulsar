// File: Pulsar.Compiler/Generated/RuntimeConfig.cs
using System;
// Generated code - do not modify directly
// Generated at: 2025-02-09 23:26:45 UTC


using Serilog.Events;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pulsar.Runtime.Rules
{
    public class RuntimeConfig
    {
        private string _redisConnectionString = "localhost:6379";

        [JsonPropertyName("RedisConnectionString")]
        public string RedisConnectionString
        {
            get => _redisConnectionString;
            set => _redisConnectionString = string.IsNullOrEmpty(value) ? "localhost:6379" : value;
        }

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
