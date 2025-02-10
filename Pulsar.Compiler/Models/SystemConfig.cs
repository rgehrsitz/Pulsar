// File: Pulsar.Compiler/Models/SystemConfig.cs

using YamlDotNet.Serialization;

namespace Pulsar.Compiler.Models
{
    public class SystemConfig
    {
        [YamlMember(Alias = "version")]
        public int Version { get; set; }

        [YamlMember(Alias = "validSensors")] // Updated alias to match YAML key
        public List<string> ValidSensors { get; set; } = new();

        [YamlMember(Alias = "cycleTime")]
        public int CycleTime { get; set; } = 100;  // Default 100ms

        [YamlMember(Alias = "redisConnection")]
        public string RedisConnection { get; set; } = "localhost:6379";

        [YamlMember(Alias = "bufferCapacity")]
        public int BufferCapacity { get; set; } = 100;
    }
}