// File: Pulsar.Compiler/Generation/CodeGenHelpers.cs

using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Pulsar.Compiler.Models;
using Serilog;
using Pulsar.Compiler;

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
            
            // Add standard using directives needed for AOT
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Diagnostics.CodeAnalysis;");
            sb.AppendLine("using System.Runtime.CompilerServices;");
            sb.AppendLine();
            
            // Add attributes to preserve Redis types
            sb.AppendLine("[assembly: DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(StackExchange.Redis.ConnectionMultiplexer))]");
            sb.AppendLine("[assembly: DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(StackExchange.Redis.IDatabase))]");
            sb.AppendLine("[assembly: DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(StackExchange.Redis.HashEntry))]");
            
            // Add attributes to preserve buffer types for temporal rules
            sb.AppendLine($"[assembly: DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof({namespace1}.Buffers.RingBufferManager))]");
            sb.AppendLine($"[assembly: DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof({namespace1}.Buffers.CircularBuffer))]");
            
            // Add attributes to preserve rule interfaces
            sb.AppendLine($"[assembly: DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof({namespace1}.Interfaces.IRuleCoordinator))]");
            sb.AppendLine($"[assembly: DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof({namespace1}.Interfaces.IRuleGroup))]");
            
            // Add attributes to preserve metrics types
            sb.AppendLine("[assembly: DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Prometheus.Metrics))]");
            sb.AppendLine("[assembly: DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Prometheus.Counter))]");
            sb.AppendLine("[assembly: DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Prometheus.Histogram))]");
            
            // Add attributes to preserve service types
            sb.AppendLine($"[assembly: DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof({namespace1}.Services.RedisService))]");
            
            return sb.ToString();
        }

        /// <summary>
        /// Generates a standard file header comment including the file name and UTC generation timestamp.
        /// </summary>
        public static string GenerateFileHeader(string fileName)
        {
            return $"// Auto-generated file: {fileName}\n// Generated at: {DateTime.UtcNow.ToString("u")}\n";
        }

        /// <summary>
        /// Wraps the provided content in a namespace declaration.
        /// </summary>
        public static string WrapInNamespace(string namespaceName, string content)
        {
            return $"namespace {namespaceName}\n{{\n{content}\n}}";
        }

        /// <summary>
        /// Prepends common using statements to the content. Adjust as needed for your project.
        /// </summary>
        public static string PrependCommonUsings(string content)
        {
            string usings = "using System;\nusing System.Collections.Generic;\nusing System.IO;\n";
            return usings + "\n" + content;
        }

        /// <summary>
        /// Embeds source tracking comments that reference original rule sources into the generated content.
        /// </summary>
        public static string EmbedSourceTrackingComments(string content, string sourceReference)
        {
            string comment = $"// Source: {sourceReference}\n";
            return comment + content;
        }

        public static string GenerateConditionCode(ConditionGroup conditions, string indent = "    ")
        {
            try
            {
                _logger.Debug("Generating condition code for {Count} conditions", conditions?.All?.Count ?? 0);
                var builder = new StringBuilder();
                
                if (conditions?.All != null && conditions.All.Any())
                {
                    foreach (var condition in conditions.All)
                    {
                        builder.AppendLine($"{indent}{GenerateConditionExpression(condition)}");
                    }
                }

                _logger.Debug("Successfully generated condition code");
                return builder.ToString();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to generate condition code");
                throw;
            }
        }

        public static string GenerateActionCode(List<ActionDefinition> actions, string indent = "    ")
        {
            try
            {
                _logger.Debug("Generating action code for {Count} actions", actions?.Count ?? 0);
                var builder = new StringBuilder();

                if (actions != null)
                {
                    foreach (var action in actions)
                    {
                        builder.AppendLine($"{indent}{GenerateActionExpression(action)}");
                    }
                }

                _logger.Debug("Successfully generated action code");
                return builder.ToString();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to generate action code");
                throw;
            }
        }

        private static string GenerateConditionExpression(ConditionDefinition condition)
        {
            _logger.Debug("Generating expression for condition type: {Type}", condition.GetType().Name);
            
            // Default implementation - should be overridden for specific condition types
            throw new NotImplementedException($"Generation of condition type {condition.GetType().Name} is not implemented");
        }

        private static string GenerateActionExpression(ActionDefinition action)
        {
            _logger.Debug("Generating expression for action type: {Type}", action.GetType().Name);
            
            // Default implementation - should be overridden for specific action types
            throw new NotImplementedException($"Generation of action type {action.GetType().Name} is not implemented");
        }

        public static string SanitizeIdentifier(string identifier)
        {
            try
            {
                _logger.Debug("Sanitizing identifier: {Identifier}", identifier);
                var sanitized = string.Concat(identifier.Split(Path.GetInvalidFileNameChars()));
                _logger.Debug("Sanitized result: {Result}", sanitized);
                return sanitized;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to sanitize identifier: {Identifier}", identifier);
                throw;
            }
        }
    }
}