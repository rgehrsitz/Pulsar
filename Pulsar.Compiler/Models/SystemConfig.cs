// File: Pulsar.Compiler/Models/SystemConfig.cs

using System.Collections.Generic;

namespace Pulsar.Compiler.Models
{
    public class SystemConfig
    {
        public int Version { get; set; }
        public List<string> ValidSensors { get; set; } = new();
    }
}