// File: Pulsar.Compiler/Generation/Generators/RuleGroupGenerator.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Pulsar.Compiler.Config;
using Pulsar.Compiler.Generation.Helpers;
using Pulsar.Compiler.Models;

namespace Pulsar.Compiler.Generation.Generators
{
    public class RuleGroupGenerator
    {
        private readonly ILogger _logger;

        public RuleGroupGenerator(ILogger logger = null)
        {
            _logger = logger ?? NullLogger.Instance;
        }

        public GeneratedFileInfo GenerateGroupImplementation(
            int groupId,
            List<RuleDefinition> rules,
            Dictionary<string, string> layerMap,
            BuildConfig buildConfig
        )
        {
            var sb = new StringBuilder();
            sb.AppendLine("// Auto-generated rule group");
            sb.AppendLine("// Generated: " + DateTime.UtcNow.ToString("O"));
            sb.AppendLine();

            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine("using Microsoft.Extensions.Logging;");
            sb.AppendLine("using Prometheus;");
            sb.AppendLine("using StackExchange.Redis;");
            sb.AppendLine("using Beacon.Runtime.Buffers;");
            sb.AppendLine("using Beacon.Runtime.Rules;");
            sb.AppendLine("using Beacon.Runtime.Interfaces;");
            sb.AppendLine("using Beacon.Runtime.Services;");
            sb.AppendLine();

            sb.AppendLine($"namespace {buildConfig.Namespace}");
            sb.AppendLine("{");

            // Class declaration
            sb.AppendLine($"    public class RuleGroup{groupId} : IRuleGroup");
            sb.AppendLine("    {");

            // Properties
            sb.AppendLine("        public IRedisService Redis { get; }");
            sb.AppendLine("        public Microsoft.Extensions.Logging.ILogger Logger { get; }");
            sb.AppendLine("        public RingBufferManager BufferManager { get; }");
            sb.AppendLine();

            // Constructor
            sb.AppendLine($"        public RuleGroup{groupId}(");
            sb.AppendLine("            IRedisService redis,");
            sb.AppendLine("            Microsoft.Extensions.Logging.ILogger logger,");
            sb.AppendLine("            RingBufferManager bufferManager)");
            sb.AppendLine("        {");
            sb.AppendLine("            Redis = redis;");
            sb.AppendLine("            Logger = logger;");
            sb.AppendLine("            BufferManager = bufferManager;");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Required sensors property
            var requiredSensors = rules
                .SelectMany(r => GenerationHelpers.GetInputSensors(r))
                .Distinct()
                .ToList();
            sb.AppendLine("        public string[] RequiredSensors => new[]");
            sb.AppendLine("        {");
            foreach (var sensor in requiredSensors)
            {
                sb.AppendLine($"            \"{sensor}\",");
            }
            sb.AppendLine("        };");
            sb.AppendLine();

            // Rule evaluation method
            sb.AppendLine("        public async Task EvaluateRulesAsync(");
            sb.AppendLine("            Dictionary<string, object> inputs,");
            sb.AppendLine("            Dictionary<string, object> outputs)");
            sb.AppendLine("        {");

            foreach (var rule in rules)
            {
                // Add rule metadata as comments
                sb.AppendLine($"            // Rule: {rule.Name}");
                sb.AppendLine($"            // Layer: {layerMap[rule.Name]}");
                sb.AppendLine($"            // Source: {rule.SourceFile}:{rule.LineNumber}");
                sb.AppendLine();

                // Generate condition check
                if (rule.Conditions != null)
                {
                    sb.AppendLine(
                        $"            if ({GenerationHelpers.GenerateCondition(rule.Conditions)})"
                    );
                    sb.AppendLine("            {");

                    // Generate actions
                    if (rule.Actions != null)
                    {
                        foreach (var action in rule.Actions)
                        {
                            sb.AppendLine(
                                $"                {GenerationHelpers.GenerateAction(action)}"
                            );
                        }
                    }

                    sb.AppendLine("            }");
                }
                else
                {
                    // If no conditions, always execute actions
                    if (rule.Actions != null)
                    {
                        foreach (var action in rule.Actions)
                        {
                            sb.AppendLine(
                                $"            {GenerationHelpers.GenerateAction(action)}"
                            );
                        }
                    }
                }

                sb.AppendLine();
            }

            sb.AppendLine("            await Task.CompletedTask;");
            sb.AppendLine("        }");

            // Add helper methods for threshold checking
            sb.AppendLine();
            sb.AppendLine(
                "        private bool CheckThreshold(string sensor, double threshold, int duration, string comparisonOperator)"
            );
            sb.AppendLine("        {");
            sb.AppendLine(
                "            // Implementation of threshold checking using BufferManager"
            );
            sb.AppendLine(
                "            var values = BufferManager.GetValues(sensor, TimeSpan.FromMilliseconds(duration));"
            );
            sb.AppendLine("            if (values.Count == 0) return false;");
            sb.AppendLine();
            sb.AppendLine("            switch (comparisonOperator)");
            sb.AppendLine("            {");
            sb.AppendLine(
                "                case \">\": return values.All(v => Convert.ToDouble(v.Value) > threshold);"
            );
            sb.AppendLine(
                "                case \"<\": return values.All(v => Convert.ToDouble(v.Value) < threshold);"
            );
            sb.AppendLine(
                "                case \">=\": return values.All(v => Convert.ToDouble(v.Value) >= threshold);"
            );
            sb.AppendLine(
                "                case \"<=\": return values.All(v => Convert.ToDouble(v.Value) <= threshold);"
            );
            sb.AppendLine(
                "                case \"==\": return values.All(v => Convert.ToDouble(v.Value) == threshold);"
            );
            sb.AppendLine(
                "                case \"!=\": return values.All(v => Convert.ToDouble(v.Value) != threshold);"
            );
            sb.AppendLine(
                "                default: throw new ArgumentException($\"Unsupported comparison operator: {comparisonOperator}\");"
            );
            sb.AppendLine("            }");
            sb.AppendLine("        }");

            sb.AppendLine("    }");
            sb.AppendLine("}");

            return new GeneratedFileInfo
            {
                FileName = $"RuleGroup{groupId}.cs",
                Content = sb.ToString(),
                Namespace = buildConfig.Namespace,
            };
        }
    }
}
