// File: Pulsar.Compiler/Models/CompilationResult.cs

using System.Reflection;

namespace Pulsar.Compiler.Models
{
    public class CompilationResult
    {
        public bool Success { get; set; }
        public string[] Errors { get; set; } = new string[0];
        public string[] GeneratedFiles { get; set; } = new string[0];
        public Assembly? Assembly { get; set; }
    }
}
