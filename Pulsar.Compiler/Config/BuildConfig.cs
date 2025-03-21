// File: Pulsar.Compiler/Config/BuildConfig.cs

using Pulsar.Compiler.Models;

namespace Pulsar.Compiler.Config
{
    public class BuildConfig
    {
        public required string OutputPath { get; set; }
        public string OutputDirectory => OutputPath; // new alias for backward compatibility
        public required string Target { get; set; }
        public required string ProjectName { get; set; }
        public string AssemblyName { get; set; } = "Runtime"; // Added for AOT compilation
        public required string TargetFramework { get; set; }
        public required string RulesPath { get; set; } = string.Empty;

        // Added for handling list of rule definitions directly
        public List<RuleDefinition> RuleDefinitions { get; set; } = new List<RuleDefinition>();
        public SystemConfig? SystemConfig { get; set; }

        public string Namespace { get; set; } = "Generated";
        public bool Parallel { get; set; } = true;
        public bool GenerateDebugInfo { get; set; }
        public bool StandaloneExecutable { get; set; }
        public bool OptimizeOutput { get; set; } = true;
        public int ComplexityThreshold { get; set; } = 10;
        public int MaxRulesPerFile { get; set; } = 100;
        public bool GroupParallelRules { get; set; } = true;

        // Missing properties needed for Beacon AOT implementation
        public bool GenerateTestProject { get; set; } = true;
        public bool CreateSeparateDirectory { get; set; } = true;
        public string SolutionName { get; set; } = "Beacon";

        // New properties referenced by CodeGenerator and others:
        public string RedisConnection { get; set; } = "localhost:6379";
        public int CycleTime { get; set; } = 100;
        public int BufferCapacity { get; set; } = 100;
        public string AdditionalUsings { get; set; } = "";
        public int MaxLinesPerFile { get; set; } = 1000;

        // Default constructor
    }

    public class BuildResult(bool success = true)
    {
        public bool Success { get; set; } = success;
        public string[] Errors { get; set; } = Array.Empty<string>();
        public string[] Warnings { get; set; } = Array.Empty<string>();
        public string[] GeneratedFiles { get; set; } = Array.Empty<string>();
        public Dictionary<string, RuleMetrics> RuleMetrics { get; set; } =
            new Dictionary<string, RuleMetrics>();
        public RuleManifest Manifest { get; set; } = new RuleManifest();
        public string OutputPath { get; set; } = string.Empty;
        public RuleMetrics Metrics { get; set; } = new RuleMetrics();

        public static BuildResult Failed(params string[] errors)
        {
            return new BuildResult(false) { Errors = errors };
        }

        public static BuildResult Succeeded(string[] generatedFiles)
        {
            return new BuildResult(true) { GeneratedFiles = generatedFiles };
        }
    }

    public class RuleMetrics
    {
        /// <summary>
        /// Estimated complexity of the rule
        /// </summary>
        public int EstimatedComplexity { get; set; }

        /// <summary>
        /// Depth of rule dependencies
        /// </summary>
        public int DependencyDepth { get; set; }

        /// <summary>
        /// Input sensors used by the rule
        /// </summary>
        public List<string> InputSensors { get; } = new List<string>();

        /// <summary>
        /// Output sensors modified by the rule
        /// </summary>
        public List<string> OutputSensors { get; } = new List<string>();

        /// <summary>
        /// Indicates if the rule uses temporal conditions
        /// </summary>
        public bool UsesTemporalConditions { get; set; }
    }

    public class EnhancedBuildConfig : BuildConfig
    {
        // New properties for MSBuild integration
        public bool ForceRebuild { get; set; } = false;
        public string[] ExcludedFiles { get; set; } = Array.Empty<string>();
        public string[] IncludedFiles { get; set; } = Array.Empty<string>();

        // Enhanced incremental build tracking
        public DateTime LastSuccessfulBuild { get; set; } = DateTime.MinValue;

        // Performance and logging options
        public bool EnablePerformanceLogging { get; set; } = true;
        public LogLevel DetailedLogLevel { get; set; } = LogLevel.Information;

        // Dependency and complexity management
        public int MaxDependencyDepth { get; set; } = 5;
        public bool StrictDependencyChecking { get; set; } = true;

        // Enum for log levels
        public enum LogLevel
        {
            Debug,
            Information,
            Warning,
            Error,
            Fatal,
        }
    }
}
