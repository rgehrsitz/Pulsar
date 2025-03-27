// File: Pulsar.Compiler/Generation/CodeGenHelpers.cs

using System.Text;
using Pulsar.Compiler.Models;
using Serilog;

namespace Pulsar.Compiler.Generation
{
    /// <summary>
    /// Consolidated helper methods for code generation routines such as file header generation, namespace wrapping, common usings, and embedding source tracking comments.
    /// </summary>
    public static class CodeGenHelpers
    {
        private static readonly ILogger _logger = LoggingConfig.GetLogger();

        /// <summary>
        /// Generates AOT compatibility attributes for the assembly
        /// </summary>
        public static string GenerateAOTAttributes(string namespace1)
        {
            var sb = new StringBuilder();
            
            // Instead of assembly-level attributes, we'll generate a static class with a method
            // that has the DynamicDependency attributes applied to it
            sb.AppendLine("// AOT compatibility helpers");
            sb.AppendLine("internal static class AOTHelpers");
            sb.AppendLine("{");
            sb.AppendLine("    // This method is never called but ensures types needed for AOT compatibility are preserved");
            sb.AppendLine("    [UnconditionalSuppressMessage(\"Trimming\", \"IL2026\", Justification = \"These types are preserved for AOT compatibility\")]");
            sb.AppendLine("    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(StackExchange.Redis.ConnectionMultiplexer))]");
            sb.AppendLine("    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(StackExchange.Redis.IDatabase))]");
            sb.AppendLine("    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(StackExchange.Redis.HashEntry))]");
            sb.AppendLine($"    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof({namespace1}.Buffers.RingBufferManager))]");
            sb.AppendLine($"    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof({namespace1}.Buffers.CircularBuffer))]");
            sb.AppendLine($"    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof({namespace1}.Interfaces.IRuleCoordinator))]");
            sb.AppendLine($"    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof({namespace1}.Interfaces.IRuleGroup))]");
            sb.AppendLine("    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Prometheus.Metrics))]");
            sb.AppendLine("    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Prometheus.Counter))]");
            sb.AppendLine("    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Prometheus.Histogram))]");
            sb.AppendLine("    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Prometheus.MetricServer))]");
            sb.AppendLine($"    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof({namespace1}.Services.MetricsService))]");
            sb.AppendLine($"    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof({namespace1}.Services.RedisService))]");
            sb.AppendLine("    internal static void EnsureAOTCompatibility() { }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        // Rest of the class implementation...
    }
}
