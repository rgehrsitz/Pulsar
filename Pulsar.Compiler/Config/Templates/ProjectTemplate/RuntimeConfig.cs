// File: Pulsar.Compiler/Config/Templates/ProjectTemplate/RuntimeConfig.cs

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog.Events;

namespace Pulsar.Runtime;

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
    [JsonConverter(typeof(TimeSpanJsonConverter))]
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

public class TimeSpanJsonConverter : JsonConverter<TimeSpan?>
{
    public override TimeSpan? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();
            return string.IsNullOrEmpty(value) ? null : TimeSpan.Parse(value);
        }
        return null;
    }

    public override void Write(
        Utf8JsonWriter writer,
        TimeSpan? value,
        JsonSerializerOptions options
    )
    {
        if (value.HasValue)
        {
            writer.WriteStringValue(value.Value.ToString("c"));
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}
