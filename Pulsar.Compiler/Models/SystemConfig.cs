// File: Pulsar.Compiler/Models/SystemConfig.cs

namespace Pulsar.Compiler.Models
{
    public class SystemConfig
    {
        public int Version { get; set; }
        public List<string> ValidSensors { get; set; } = new();
        public int CycleTime { get; set; } = 100;  // Default 100ms
        public string RedisConnection { get; set; } = "localhost:6379";
        public int BufferCapacity { get; set; } = 100;
    }
}