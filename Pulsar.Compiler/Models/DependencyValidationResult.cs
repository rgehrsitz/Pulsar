// File: Pulsar.Compiler/Models/DependencyValidationResult.cs
using System.Collections.Generic;

namespace Pulsar.Compiler.Models
{
    public class DependencyValidationResult
    {
        public bool IsValid { get; set; }
        public List<List<string>> CircularDependencies { get; set; } = new();
        public List<List<string>> DeepDependencyChains { get; set; } = new();
        public Dictionary<string, int> RuleComplexityScores { get; set; } = new();
        public Dictionary<string, HashSet<string>> TemporalDependencies { get; set; } = new();
    }
}
