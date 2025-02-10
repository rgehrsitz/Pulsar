// File: Pulsar.Compiler/CodeGenerator.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Pulsar.Compiler.Config;
using Pulsar.Compiler.Models;
using Pulsar.Compiler.Parsers;
using Serilog; // Added missing using directive for ILogger

namespace Pulsar.Compiler.Generation
{
    public class RuleGroupingConfig
    {
        // Default values chosen based on typical C# file size guidelines
        public int MaxRulesPerFile { get; set; } = 100;
        public int MaxLinesPerFile { get; set; } = 1000;
        public bool GroupParallelRules { get; set; } = true;
    }

    public class CodeGenerator
    {
        public static List<GeneratedFileInfo> GenerateCSharp(
            List<RuleDefinition> rules,
            BuildConfig? config = null
        )
        {
            // Supply defaults for required members if config is null
            config ??= new BuildConfig
            {
                OutputPath = "Generated",
                Target = "win-x64",
                ProjectName = "Pulsar.Compiler",
                TargetFramework = "net9.0",
            };

            var files = config.StandaloneExecutable
                ? GenerateStandaloneProgram(rules, config)
                : GenerateRuleFiles(rules, config); // Use the existing method for non-standalone

            files = ApplyPostGenerationFixups(files);

            return files;
        }

        private static List<GeneratedFileInfo> GenerateStandaloneProgram(
            List<RuleDefinition> rules,
            BuildConfig config
        )
        {
            var files = new List<GeneratedFileInfo>();

            // Generate standard rule code first
            files.AddRange(GenerateRuleFiles(rules, config));

            // Add the Program.cs with Main entry point
            files.Add(GenerateProgramClass(config));

            // Add embedded configuration
            files.Add(GenerateEmbeddedConfig(config));

            // Add runtime configuration
            files.Add(GenerateRuntimeConfig());

            files.Add(GenerateConfigurationLoader());
            files.Add(GenerateRuntimeProjectFile(config));

            return files;
        }

        /// <summary>
        /// Adds source tracking comments to the generated file content
        /// </summary>
        private static void AddSourceTrackingComments(
            GeneratedFileInfo fileInfo,
            List<RuleDefinition> groupRules
        )
        {
            var sourceFiles = groupRules
                .Select(r => r.SourceInfo?.FileName)
                .Where(f => !string.IsNullOrEmpty(f))
                .Distinct()
                .ToList();

            // If we have source files, add a comment block at the top
            if (sourceFiles.Any())
            {
                var sourceComment = $"// Source Files: {string.Join(", ", sourceFiles)}\n";
                sourceComment += "// Rules in this group:\n";
                sourceComment += string.Join(
                    "\n",
                    groupRules.Select(r =>
                        $"//   - {r.Name} (from {r.SourceInfo?.FileName ?? "unknown"})"
                    )
                );

                fileInfo.Content = fileInfo.Content.Insert(
                    fileInfo.Content.IndexOf("namespace"),
                    sourceComment + "\n"
                );
            }
        }

        private static string GenerateCommonUsings()
        {
            var builder = new StringBuilder();
            builder.AppendLine("using System;");
            builder.AppendLine("using System.Collections.Generic;");
            builder.AppendLine("using System.Linq;");
            builder.AppendLine("using System.Threading.Tasks;");
            builder.AppendLine("using Serilog;");
            builder.AppendLine("using Prometheus;");
            builder.AppendLine("using Pulsar.Runtime.Buffers;");
            builder.AppendLine("using Pulsar.Runtime.Common;");
            return builder.ToString();
        }

        private static GeneratedFileInfo GenerateCompiledRulesClass(
            Dictionary<int, List<RuleDefinition>> rulesByLayer
        )
        {
            var builder = new StringBuilder();
            builder.AppendLine(GenerateFileHeader());
            builder.AppendLine(GenerateCommonUsings());
            builder.AppendLine("namespace Pulsar.Runtime.Rules");
            builder.AppendLine("{");
            builder.AppendLine("    public partial class CompiledRules : ICompiledRules");
            builder.AppendLine("    {");
            builder.AppendLine("        private readonly ILogger _logger;");
            builder.AppendLine("        private readonly RingBufferManager _bufferManager;");
            builder.AppendLine();
            builder.AppendLine(
                "        public CompiledRules(ILogger logger, RingBufferManager bufferManager)"
            );
            builder.AppendLine("        {");
            builder.AppendLine(
                "            _logger = logger ?? throw new ArgumentNullException(nameof(logger));"
            );
            builder.AppendLine(
                "            _bufferManager = bufferManager ?? throw new ArgumentNullException(nameof(bufferManager));"
            );
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine(
                "        public void Evaluate(Dictionary<string, double> inputs, Dictionary<string, double> outputs, RingBufferManager bufferManager)"
            );
            builder.AppendLine("        {");
            builder.AppendLine("            try");
            builder.AppendLine("            {");
            builder.AppendLine("                _logger.Debug(\"Starting rule evaluation\");");

            // Call each layer's evaluation method in order
            foreach (var layer in rulesByLayer.Keys.OrderBy(k => k))
            {
                builder.AppendLine(
                    $"                EvaluateLayer{layer}(inputs, outputs, bufferManager);"
                );
            }

            builder.AppendLine("                _logger.Debug(\"Rule evaluation completed\");");
            builder.AppendLine("            }");
            builder.AppendLine("            catch (Exception ex)");
            builder.AppendLine("            {");
            builder.AppendLine(
                "                _logger.Error(ex, \"Error during rule evaluation\");"
            );
            builder.AppendLine("                throw;");
            builder.AppendLine("            }");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine("}");

            return new GeneratedFileInfo
            {
                FileName = "CompiledRules.cs",
                Content = builder.ToString(),
                Namespace = "Pulsar.Runtime.Rules",
                LayerRange = new RuleLayerRange
                {
                    Start = rulesByLayer.Keys.Any() ? rulesByLayer.Keys.Min() : 0,
                    End = rulesByLayer.Keys.Any() ? rulesByLayer.Keys.Max() : 0,
                },
            };
        }

        private static GeneratedFileInfo GenerateLayerImplementation(
            int layer,
            List<RuleDefinition> rules
        )
        {
            var builder = new StringBuilder();
            builder.AppendLine(GenerateFileHeader());
            builder.AppendLine(GenerateCommonUsings());
            builder.AppendLine("namespace Pulsar.Runtime.Rules");
            builder.AppendLine("{");
            builder.AppendLine("    public partial class CompiledRules");
            builder.AppendLine("    {");
            builder.AppendLine(
                $"        private void EvaluateLayer{layer}(Dictionary<string, double> inputs, Dictionary<string, double> outputs, RingBufferManager bufferManager)"
            );
            builder.AppendLine("        {");
            builder.AppendLine($"            _logger.Debug(\"Evaluating layer {layer}\");");
            builder.AppendLine("            try");
            builder.AppendLine("            {");

            // Generate each rule's implementation within this layer
            foreach (var rule in rules)
            {
                // Add source tracking
                if (rule.SourceInfo != null)
                {
                    builder.AppendLine(
                        $"                // Source: {rule.SourceInfo.FileName}:{rule.SourceInfo.LineNumber}"
                    );
                }
                builder.AppendLine($"                // Rule: {rule.Name}");
                if (!string.IsNullOrEmpty(rule.Description))
                {
                    builder.AppendLine($"                // Description: {rule.Description}");
                }

                // Add debug logging
                builder.AppendLine(
                    $"                _logger.Debug(\"Evaluating rule {rule.Name}\");"
                );

                string condition = GenerateCondition(rule.Conditions);
                // Only generate if statement if there are actual conditions
                if (!string.IsNullOrEmpty(condition) && condition != "true")
                {
                    builder.AppendLine($"                if ({condition})");
                    builder.AppendLine("                {");
                    GenerateActions(builder, rule.Actions, "                    ");
                    builder.AppendLine("                }");
                }
                else
                {
                    GenerateActions(builder, rule.Actions, "                "); // Uses default indent
                }
            }

            builder.AppendLine("            }");
            builder.AppendLine("            catch (Exception ex)");
            builder.AppendLine("            {");
            builder.AppendLine(
                $"                _logger.Error(ex, \"Error evaluating layer {layer}\");"
            );
            builder.AppendLine("                throw;");
            builder.AppendLine("            }");
            builder.AppendLine($"            _logger.Debug(\"Completed layer {layer}\");");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine("}");

            return new GeneratedFileInfo
            {
                FileName = $"CompiledRules.Layer{layer}.cs",
                Content = builder.ToString(),
                Namespace = "Pulsar.Runtime.Rules",
                LayerRange = new RuleLayerRange { Start = layer, End = layer },
            };
        }

        private static void GenerateActions(
            StringBuilder builder,
            List<ActionDefinition> actions,
            string indent = "            "
        )
        {
            foreach (var action in actions.OfType<SetValueAction>())
            {
                string valueAssignment;
                if (action.Value.HasValue)
                {
                    valueAssignment = action.Value.Value.ToString(
                        System.Globalization.CultureInfo.InvariantCulture
                    );
                }
                else if (!string.IsNullOrEmpty(action.ValueExpression))
                {
                    var fixedExpression = FixupExpression(action.ValueExpression);
                    valueAssignment = fixedExpression;
                }
                else
                {
                    valueAssignment = "0";
                }

                builder.AppendLine($"{indent}outputs[\"{action.Key}\"] = {valueAssignment};");
            }
        }

        private static string GetOperator(ComparisonOperator op)
        {
            return op switch
            {
                ComparisonOperator.LessThan => "<",
                ComparisonOperator.GreaterThan => ">",
                ComparisonOperator.LessThanOrEqual => "<=",
                ComparisonOperator.GreaterThanOrEqual => ">=",
                ComparisonOperator.EqualTo => "==",
                ComparisonOperator.NotEqualTo => "!=",
                _ => throw new InvalidOperationException($"Unsupported operator: {op}"),
            };
        }

        internal static string FixupExpression(string expression)
        {
            if (string.IsNullOrEmpty(expression))
            {
                return expression;
            }

            // List of Math functions to preserve
            var mathFunctions = new[]
            {
                "Math.Abs",
                "Math.Pow",
                "Math.Sqrt",
                "Math.Sin",
                "Math.Cos",
                "Math.Tan",
                "Math.Log",
                "Math.Exp",
                "Math.Floor",
                "Math.Ceiling",
                "Math.Round",
            };

            // First handle Math function calls - preserve case
            foreach (var func in mathFunctions)
            {
                expression = expression.Replace(func.ToLower(), func);
            }

            // Fix: Correctly wrap variables in inputs[] access while preserving existing parentheses
            var wrappedExpression = Regex.Replace(
                expression,
                @"\b(?!Math\.)([a-zA-Z_][a-zA-Z0-9_]*)\b(?!\[|\()",
                "inputs[\"$1\"]"
            );

            return wrappedExpression;
        }

        private static GeneratedFileInfo GenerateEmptyCompiledRules()
        {
            var builder = new StringBuilder();
            builder.AppendLine(GenerateFileHeader());
            builder.AppendLine(GenerateCommonUsings());
            builder.AppendLine("namespace Pulsar.Runtime.Rules");
            builder.AppendLine("{");
            builder.AppendLine("    public class CompiledRules : ICompiledRules");
            builder.AppendLine("    {");
            builder.AppendLine("        private readonly ILogger _logger;");
            builder.AppendLine("        private readonly RingBufferManager _bufferManager;");
            builder.AppendLine();
            builder.AppendLine(
                "        public CompiledRules(ILogger logger, RingBufferManager bufferManager)"
            );
            builder.AppendLine("        {");
            builder.AppendLine(
                "            _logger = logger ?? throw new ArgumentNullException(nameof(logger));"
            );
            builder.AppendLine(
                "            _bufferManager = bufferManager ?? throw new ArgumentNullException(nameof(bufferManager));"
            );
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine(
                "        public void Evaluate(Dictionary<string, double> inputs, Dictionary<string, double> outputs, RingBufferManager bufferManager)"
            );
            builder.AppendLine("        {");
            builder.AppendLine("            _logger.Debug(\"No rules to evaluate\");");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine("}");

            return new GeneratedFileInfo
            {
                FileName = "CompiledRules.cs",
                Content = builder.ToString(),
                Namespace = "Pulsar.Runtime.Rules",
            };
        }

        private static string GenerateFileHeader()
        {
            return @"// Generated code - do not modify directly
// Generated at: "
                + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC")
                + @"
";
        }

        private static GeneratedFileInfo GenerateInterfaceFile()
        {
            var builder = new StringBuilder();
            builder.AppendLine(GenerateFileHeader());
            builder.AppendLine(GenerateCommonUsings());
            builder.AppendLine("namespace Pulsar.Runtime.Rules");
            builder.AppendLine("{");
            builder.AppendLine("    public interface ICompiledRules");
            builder.AppendLine("    {");
            builder.AppendLine(
                "        void Evaluate(Dictionary<string, double> inputs, Dictionary<string, double> outputs, RingBufferManager bufferManager);"
            );
            builder.AppendLine("    }");
            builder.AppendLine("}");

            return new GeneratedFileInfo
            {
                FileName = "ICompiledRules.cs",
                Content = builder.ToString(),
                Namespace = "Pulsar.Runtime.Rules",
            };
        }

        private static Dictionary<string, int> AssignLayers(List<RuleDefinition> rules)
        {
            var layerMap = new Dictionary<string, int>();
            var dependencyGraph = BuildDependencyGraph(rules);
            var visited = new HashSet<string>();
            var visiting = new HashSet<string>();

            foreach (var rule in rules)
            {
                if (!visited.Contains(rule.Name))
                {
                    AssignLayerDFS(rule.Name, dependencyGraph, layerMap, visited, visiting);
                }
            }

            return layerMap;
        }

        private static Dictionary<string, HashSet<string>> BuildDependencyGraph(
            List<RuleDefinition> rules
        )
        {
            var graph = new Dictionary<string, HashSet<string>>();
            var outputRules = new Dictionary<string, string>();

            // Initialize graph
            foreach (var rule in rules)
            {
                graph[rule.Name] = new HashSet<string>();

                // Record outputs
                foreach (var action in rule.Actions.OfType<SetValueAction>())
                {
                    outputRules[action.Key] = rule.Name;
                }
            }

            // Build dependencies
            foreach (var rule in rules)
            {
                var dependencies = GetDependencies(rule, outputRules);
                foreach (var dep in dependencies)
                {
                    graph[rule.Name].Add(dep);
                }
            }

            return graph;
        }

        private static void AssignLayerDFS(
            string ruleName,
            Dictionary<string, HashSet<string>> graph,
            Dictionary<string, int> layerMap,
            HashSet<string> visited,
            HashSet<string> visiting
        )
        {
            if (visiting.Contains(ruleName))
            {
                throw new InvalidOperationException(
                    $"Cyclic dependency detected involving rule '{ruleName}'"
                );
            }

            if (visited.Contains(ruleName))
            {
                return;
            }

            visiting.Add(ruleName);

            int maxDependencyLayer = -1;
            foreach (var dependency in graph[ruleName])
            {
                if (!layerMap.ContainsKey(dependency))
                {
                    AssignLayerDFS(dependency, graph, layerMap, visited, visiting);
                }
                maxDependencyLayer = Math.Max(maxDependencyLayer, layerMap[dependency]);
            }

            layerMap[ruleName] = maxDependencyLayer + 1;
            visiting.Remove(ruleName);
            visited.Add(ruleName);
        }

        private static List<string> GetDependencies(
            RuleDefinition rule,
            Dictionary<string, string> outputRules
        )
        {
            var dependencies = new HashSet<string>();

            void AddConditionDependencies(ConditionDefinition condition)
            {
                if (condition is ComparisonCondition comp)
                {
                    if (outputRules.TryGetValue(comp.Sensor, out var ruleName))
                    {
                        dependencies.Add(ruleName);
                    }
                }
                else if (condition is ExpressionCondition expr)
                {
                    foreach (var (sensor, ruleName) in outputRules)
                    {
                        if (expr.Expression.Contains(sensor))
                        {
                            dependencies.Add(ruleName);
                        }
                    }
                }
            }

            if (rule.Conditions?.All != null)
            {
                foreach (var condition in rule.Conditions.All)
                {
                    AddConditionDependencies(condition);
                }
            }

            if (rule.Conditions?.Any != null)
            {
                foreach (var condition in rule.Conditions.Any)
                {
                    AddConditionDependencies(condition);
                }
            }

            return dependencies.ToList();
        }

        private static Dictionary<int, List<RuleDefinition>> GetRulesByLayer(
            List<RuleDefinition> rules,
            Dictionary<string, int> layerMap
        )
        {
            return rules.GroupBy(r => layerMap[r.Name]).ToDictionary(g => g.Key, g => g.ToList());
        }

        private static GeneratedFileInfo GenerateRuleImplementation(RuleDefinition rule, int layer)
        {
            var builder = new StringBuilder();
            builder.AppendLine(GenerateFileHeader());
            builder.AppendLine(GenerateCommonUsings());
            builder.AppendLine("namespace Pulsar.Runtime.Rules");
            builder.AppendLine("{");
            builder.AppendLine("    public partial class CompiledRules");
            builder.AppendLine("    {");

            // Rule implementation
            builder.AppendLine($"        // Rule: {rule.Name}");
            if (!string.IsNullOrEmpty(rule.Description))
            {
                builder.AppendLine($"        // Description: {rule.Description}");
            }

            string condition = GenerateCondition(rule.Conditions);
            builder.AppendLine(
                $"        private void Evaluate_{rule.Name}(Dictionary<string, double> inputs, Dictionary<string, double> outputs, RingBufferManager bufferManager)"
            );
            builder.AppendLine("        {");
            if (!string.IsNullOrEmpty(condition))
            {
                builder.AppendLine($"            if ({condition})");
                builder.AppendLine("            {");
                GenerateActions(builder, rule.Actions, "                ");
                builder.AppendLine("            }");
            }
            else
            {
                GenerateActions(builder, rule.Actions, "            ");
            }
            builder.AppendLine("        }");

            builder.AppendLine("    }");
            builder.AppendLine("}");

            return new GeneratedFileInfo
            {
                FileName = $"Rule_{rule.Name}.cs",
                Content = builder.ToString(),
                Namespace = "Pulsar.Runtime.Rules",
                LayerRange = new RuleLayerRange { Start = layer, End = layer },
            };
        }

        private static GeneratedFileInfo GenerateMetadataFile(
            List<RuleDefinition> rules,
            Dictionary<string, int> layerMap
        )
        {
            var metadata = new RuleManifest
            {
                GeneratedAt = DateTime.UtcNow,
                SchemaVersion = "1.0",
            };

            foreach (var rule in rules)
            {
                metadata.Rules[rule.Name] = new RuleMetadata
                {
                    SourceFile = rule.SourceInfo?.FileName ?? "unknown",
                    SourceLineNumber = rule.SourceInfo?.LineNumber ?? 0,
                    Layer = layerMap[rule.Name],
                    Description = rule.Description,
                    InputSensors = GetInputSensors(rule),
                    OutputSensors = GetOutputSensors(rule),
                };
            }

            var json = System.Text.Json.JsonSerializer.Serialize(
                metadata,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }
            );

            return new GeneratedFileInfo
            {
                FileName = "rules.manifest.json",
                Content = json,
                Namespace = "Pulsar.Runtime.Rules",
            };
        }

        private static string GenerateCondition(ConditionGroup? conditions, bool isPartOfOr = false)
        {
            if (
                conditions == null
                || (!conditions.All?.Any() ?? true) && (!conditions.Any?.Any() ?? true)
            )
            {
                return string.Empty;
            }

            var parts = new List<string>();

            if (conditions.All?.Any() == true)
            {
                var expressions = conditions.All.Select(c => GenerateConditionExpression(c, false));
                parts.Add(string.Join(" && ", expressions));
            }

            if (conditions.Any?.Any() == true)
            {
                var expressions = conditions.Any.Select(c => GenerateConditionExpression(c, true));
                parts.Add($"({string.Join(" || ", expressions)})");
            }

            var result = string.Join(" && ", parts);
            return isPartOfOr ? $"({result})" : result;
        }

        private static string GenerateConditionExpression(
            ConditionDefinition condition,
            bool isPartOfOr = false
        )
        {
            string expression = condition switch
            {
                ComparisonCondition comp =>
                    $"inputs[\"{comp.Sensor}\"] {GetOperator(comp.Operator)} {comp.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                ExpressionCondition expr => FixupExpression(expr.Expression),
                ThresholdOverTimeCondition threshold =>
                    $"_bufferManager.IsAboveThresholdForDuration(\"{threshold.Sensor}\", {threshold.Threshold}, TimeSpan.FromMilliseconds({threshold.Duration}))",
                ConditionGroup group => GenerateCondition(group, isPartOfOr),
                _ => throw new NotSupportedException(
                    $"Unsupported condition type: {condition.GetType().Name}"
                ),
            };

            return expression;
        }

        private static List<string> GetInputSensors(RuleDefinition rule)
        {
            var sensors = new HashSet<string>();

            void AddConditionSensors(ConditionDefinition condition)
            {
                if (condition is ComparisonCondition comp)
                {
                    sensors.Add(comp.Sensor);
                }
            }

            if (rule.Conditions?.All != null)
            {
                foreach (var condition in rule.Conditions.All)
                {
                    AddConditionSensors(condition);
                }
            }

            if (rule.Conditions?.Any != null)
            {
                foreach (var condition in rule.Conditions.Any)
                {
                    AddConditionSensors(condition);
                }
            }

            return sensors.ToList();
        }

        private static List<string> GetOutputSensors(RuleDefinition rule)
        {
            return rule.Actions.OfType<SetValueAction>().Select(a => a.Key).ToList();
        }

        private static Dictionary<int, List<RuleDefinition>> SplitRulesIntoGroups(
            List<RuleDefinition> rules,
            Dictionary<string, int> layerMap,
            RuleGroupingConfig config
        )
        {
            var groups = new Dictionary<int, List<RuleDefinition>>();
            var currentGroupIndex = 0;

            // Sort rules by layer to maintain dependency order
            var sortedRules = rules.OrderBy(r => layerMap[r.Name]).ToList();

            // Group rules by layer first
            var rulesByLayer = sortedRules
                .GroupBy(r => layerMap[r.Name])
                .OrderBy(g => g.Key)
                .ToList();

            if (config.GroupParallelRules)
            {
                // When grouping parallel rules, try to keep rules from the same layer together
                foreach (var layer in rulesByLayer)
                {
                    var layerRules = layer.ToList();
                    var remainingRules = layerRules.Count;
                    var currentIndex = 0;

                    while (remainingRules > 0)
                    {
                        var currentRules = new List<RuleDefinition>();
                        var rulesInThisGroup = Math.Min(remainingRules, config.MaxRulesPerFile);

                        // Add rules from this layer up to MaxRulesPerFile
                        currentRules.AddRange(layerRules.Skip(currentIndex).Take(rulesInThisGroup));

                        groups[currentGroupIndex++] = currentRules;
                        currentIndex += rulesInThisGroup;
                        remainingRules -= rulesInThisGroup;
                    }
                }
            }
            else
            {
                // Original logic for non-parallel grouping
                var currentRules = new List<RuleDefinition>();
                var currentLayer = layerMap[sortedRules.First().Name];

                foreach (var rule in sortedRules)
                {
                    var ruleLayer = layerMap[rule.Name];

                    // Start a new group if:
                    // 1. Current group has reached max size, or
                    // 2. Current rule is in a different layer than previous rules and current group is not empty
                    if (
                        currentRules.Count >= config.MaxRulesPerFile
                        || (ruleLayer != currentLayer && currentRules.Any())
                    )
                    {
                        groups[currentGroupIndex] = currentRules;
                        currentRules = new List<RuleDefinition>();
                        currentGroupIndex++;
                    }

                    currentRules.Add(rule);
                    currentLayer = ruleLayer;
                }

                if (currentRules.Any())
                {
                    groups[currentGroupIndex] = currentRules;
                }
            }

            Log.Information(
                "[CodeGenerator] RuleGroupingConfig: MaxRulesPerFile = {MaxRulesPerFile}, GroupParallelRules = {GroupParallelRules}",
                config.MaxRulesPerFile,
                config.GroupParallelRules
            );
            Log.Information(
                "[CodeGenerator] Total rule groups generated: {GroupCount}",
                groups.Count
            );
            foreach (var kvp in groups)
            {
                Log.Information(
                    "[CodeGenerator] Group {GroupIndex} has {RuleCount} rules.",
                    kvp.Key,
                    kvp.Value.Count
                );
            }

            return groups;
        }

        private static GeneratedFileInfo GenerateGroupImplementation(
            int groupIndex,
            List<RuleDefinition> rules,
            Dictionary<string, int> layerMap
        )
        {
            var builder = new StringBuilder();
            builder.AppendLine(GenerateFileHeader());
            builder.AppendLine(GenerateCommonUsings());
            builder.AppendLine("namespace Pulsar.Runtime.Rules");
            builder.AppendLine("{");

            // Change from partial class to concrete implementation
            builder.AppendLine($"    public class RuleGroup{groupIndex} : IRuleGroup");
            builder.AppendLine("    {");
            builder.AppendLine("        private readonly ILogger _logger;");
            builder.AppendLine("        private readonly RingBufferManager _bufferManager;");
            builder.AppendLine();

            // Constructor
            builder.AppendLine(
                $"        public RuleGroup{groupIndex}(ILogger logger, RingBufferManager bufferManager)"
            );
            builder.AppendLine("        {");
            builder.AppendLine(
                "            _logger = logger ?? throw new ArgumentNullException(nameof(logger));"
            );
            builder.AppendLine(
                "            _bufferManager = bufferManager ?? throw new ArgumentNullException(nameof(bufferManager));"
            );
            builder.AppendLine("        }");
            builder.AppendLine();

            // Interface implementation
            builder.AppendLine(
                "        public void EvaluateGroup(Dictionary<string, double> inputs, Dictionary<string, double> outputs, RingBufferManager bufferManager)"
            );
            builder.AppendLine("        {");
            builder.AppendLine("            try");
            builder.AppendLine("            {");
            builder.AppendLine(
                $"                _logger.Debug(\"Evaluating rule group {groupIndex}\");"
            );

            // Generate rule implementations in layer order
            foreach (var rule in rules.OrderBy(r => layerMap[r.Name]))
            {
                // Add source tracking
                if (rule.SourceInfo != null)
                {
                    builder.AppendLine(
                        $"                // Source: {rule.SourceInfo.FileName}:{rule.SourceInfo.LineNumber}"
                    );
                }
                builder.AppendLine($"                // Rule: {rule.Name}");
                if (!string.IsNullOrEmpty(rule.Description))
                {
                    builder.AppendLine($"                // Description: {rule.Description}");
                }

                builder.AppendLine(
                    $"                _logger.Debug(\"Evaluating rule {rule.Name}\");"
                );

                string condition = GenerateCondition(rule.Conditions);
                if (!string.IsNullOrEmpty(condition) && condition != "true")
                {
                    builder.AppendLine($"                if ({condition})");
                    builder.AppendLine("                {");
                    GenerateActions(builder, rule.Actions, "                    ");
                    builder.AppendLine("                }");
                }
                else
                {
                    GenerateActions(builder, rule.Actions, "                ");
                }
            }

            builder.AppendLine("            }");
            builder.AppendLine("            catch (Exception ex)");
            builder.AppendLine("            {");
            builder.AppendLine(
                $"                _logger.Error(ex, \"Error evaluating rule group {groupIndex}\");"
            );
            builder.AppendLine("                throw;");
            builder.AppendLine("            }");
            builder.AppendLine(
                $"            _logger.Debug(\"Completed rule group {groupIndex}\");"
            );
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine("}");

            return new GeneratedFileInfo
            {
                FileName = $"RuleGroup{groupIndex}.cs",
                Content = builder.ToString(),
                Namespace = "Pulsar.Runtime.Rules",
                LayerRange = new RuleLayerRange
                {
                    Start = rules.Min(r => layerMap[r.Name]),
                    End = rules.Max(r => layerMap[r.Name]),
                },
            };
        }

        private static GeneratedFileInfo GenerateRuleCoordinator(
            Dictionary<int, List<RuleDefinition>> groups,
            Dictionary<string, int> layerMap
        )
        {
            var builder = new StringBuilder();
            builder.AppendLine(GenerateFileHeader());
            builder.AppendLine(GenerateCommonUsings());
            builder.AppendLine();
            builder.AppendLine("using System.Diagnostics.CodeAnalysis;");
            builder.AppendLine("using System.Diagnostics.Metrics;");
            builder.AppendLine();
            builder.AppendLine("namespace Pulsar.Runtime.Rules");
            builder.AppendLine("{");

            // Generate concrete coordinator class with AOT compatibility
            builder.AppendLine(
                "    [UnconditionalSuppressMessage(\"Trimming\", \"IL2026\", Justification = \"Types preserved in trimming.xml\")]"
            );
            builder.AppendLine(
                "    [UnconditionalSuppressMessage(\"Trimming\", \"IL2074\", Justification = \"Required interfaces preserved\")]"
            );
            builder.AppendLine("    public class RuleCoordinator : IRuleCoordinator");
            builder.AppendLine("    {");

            // Add performance metrics
            builder.AppendLine(
                "        private static readonly Meter s_meter = new(\"Pulsar.Runtime\");"
            );
            builder.AppendLine(
                "        private static readonly Counter<int> s_evaluationCount = s_meter.CreateCounter<int>(\"rule_evaluations_total\");"
            );
            builder.AppendLine(
                "        private static readonly Histogram<double> s_evaluationDuration = s_meter.CreateHistogram<double>(\"rule_evaluation_duration_seconds\");"
            );
            builder.AppendLine();

            // Fields for logger and each rule group
            builder.AppendLine("        private readonly ILogger _logger;");
            builder.AppendLine("        private readonly RingBufferManager _bufferManager;");

            // Add fields for each group with source tracking comments
            foreach (var group in groups.OrderBy(g => g.Key))
            {
                builder.AppendLine();
                builder.AppendLine($"        // Rules for Group {group.Key}:");
                foreach (var rule in group.Value)
                {
                    if (rule.SourceInfo != null)
                    {
                        builder.AppendLine(
                            $"        // - {rule.Name} from {rule.SourceInfo.FileName}:{rule.SourceInfo.LineNumber}"
                        );
                    }
                }
                builder.AppendLine(
                    $"        private readonly RuleGroup{group.Key} _group{group.Key};"
                );
            }

            builder.AppendLine();

            // Constructor
            builder.AppendLine(
                "        public RuleCoordinator(ILogger logger, RingBufferManager bufferManager)"
            );
            builder.AppendLine("        {");
            builder.AppendLine(
                "            _logger = logger ?? throw new ArgumentNullException(nameof(logger));"
            );
            builder.AppendLine(
                "            _bufferManager = bufferManager ?? throw new ArgumentNullException(nameof(bufferManager));"
            );
            builder.AppendLine();

            // Initialize each group with proper error handling
            foreach (var groupIndex in groups.Keys.OrderBy(k => k))
            {
                builder.AppendLine($"            try");
                builder.AppendLine("            {");
                builder.AppendLine(
                    $"                _group{groupIndex} = new RuleGroup{groupIndex}(logger, bufferManager);"
                );
                builder.AppendLine("            }");
                builder.AppendLine("            catch (Exception ex)");
                builder.AppendLine("            {");
                builder.AppendLine(
                    $"                _logger.Error(ex, \"Failed to initialize rule group {groupIndex}\");"
                );
                builder.AppendLine("                throw;");
                builder.AppendLine("            }");
            }
            builder.AppendLine("        }");
            builder.AppendLine();

            // EvaluateRules implementation from IRuleCoordinator
            builder.AppendLine(
                "        public void EvaluateRules(Dictionary<string, double> inputs, Dictionary<string, double> outputs)"
            );
            builder.AppendLine("        {");
            builder.AppendLine("            try");
            builder.AppendLine("            {");
            builder.AppendLine("                var startTime = DateTime.UtcNow;");
            builder.AppendLine("                _logger.Debug(\"Starting rule evaluation\");");
            builder.AppendLine();
            builder.AppendLine("                s_evaluationCount.Add(1);");
            builder.AppendLine();

            // Call groups in dependency order with error handling
            var orderedGroups = groups
                .OrderBy(g => g.Value.Min(r => layerMap[r.Name]))
                .Select(g => g.Key)
                .ToList();

            foreach (var groupIndex in orderedGroups)
            {
                var groupRules = groups[groupIndex];
                builder.AppendLine(
                    $"                // Layer {layerMap[groupRules[0].Name]} rules:"
                );
                foreach (var rule in groupRules)
                {
                    builder.AppendLine($"                // - {rule.Name}");
                }
                builder.AppendLine();

                builder.AppendLine($"                try");
                builder.AppendLine("                {");
                builder.AppendLine(
                    $"                    _group{groupIndex}.EvaluateGroup(inputs, outputs, _bufferManager);"
                );
                builder.AppendLine("                }");
                builder.AppendLine("                catch (Exception ex)");
                builder.AppendLine("                {");
                builder.AppendLine(
                    $"                    _logger.Error(ex, \"Error evaluating rule group {groupIndex}\");"
                );
                builder.AppendLine("                    throw;");
                builder.AppendLine("                }");
                builder.AppendLine();
            }

            // Record performance metrics
            builder.AppendLine("                var duration = DateTime.UtcNow - startTime;");
            builder.AppendLine(
                "                s_evaluationDuration.Record(duration.TotalSeconds);"
            );
            builder.AppendLine();
            builder.AppendLine(
                "                _logger.Debug(\"Completed rule evaluation in {Duration}ms\", duration.TotalMilliseconds);"
            );
            builder.AppendLine("            }");
            builder.AppendLine("            catch (Exception ex)");
            builder.AppendLine("            {");
            builder.AppendLine(
                "                _logger.Error(ex, \"Error during rule evaluation\");"
            );
            builder.AppendLine("                throw;");
            builder.AppendLine("            }");
            builder.AppendLine("        }");

            // Optional: Add debugging helper methods
            builder.AppendLine();
            builder.AppendLine("#if DEBUG");
            builder.AppendLine("        public IEnumerable<string> GetRuleNames()");
            builder.AppendLine("        {");
            builder.AppendLine("            return new[]");
            builder.AppendLine("            {");
            foreach (var group in groups)
            {
                foreach (var rule in group.Value)
                {
                    builder.AppendLine($"                \"{rule.Name}\",");
                }
            }
            builder.AppendLine("            };");
            builder.AppendLine("        }");
            builder.AppendLine("#endif");

            builder.AppendLine("    }");
            builder.AppendLine("}");

            return new GeneratedFileInfo
            {
                FileName = "RuleCoordinator.cs",
                Content = builder.ToString(),
                Namespace = "Pulsar.Runtime.Rules",
                LayerRange = new RuleLayerRange
                {
                    Start = orderedGroups.First(),
                    End = orderedGroups.Last(),
                },
            };
        }

        private static GeneratedFileInfo GenerateProgramClass(BuildConfig config)
        {
            var builder = new StringBuilder();
            builder.AppendLine(GenerateFileHeader());
            builder.AppendLine(GenerateCommonUsings());
            builder.AppendLine(
                @"
using System.Threading;
using StackExchange.Redis;
using Pulsar.Runtime;
using Pulsar.Runtime.Services;

namespace Pulsar.Runtime.Rules
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var config = ConfigurationLoader.LoadConfiguration(args);
            var logger = CreateLogger(config);

            try
            {
                logger.Information(""Starting Pulsar Runtime v{Version}"",
                    typeof(Program).Assembly.GetName().Version);

                using var redis = new RedisService(config.RedisConnectionString, logger);
                using var bufferManager = new RingBufferManager(config.BufferCapacity);

                using var orchestrator = new RuntimeOrchestrator(
                    redis,
                    logger,
                    EmbeddedConfig.ValidSensors.ToArray(),
                    LoadRuleCoordinator(config, logger, bufferManager),
                    null);

                // Setup graceful shutdown
                var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (s, e) =>
                {
                    logger.Information(""Shutdown requested, stopping gracefully..."");
                    e.Cancel = true;
                    cts.Cancel();
                };

                logger.Information(""Starting orchestrator with {SensorCount} sensors, {CycleTime}ms cycle time"",
                    EmbeddedConfig.ValidSensors.Length,
                    EmbeddedConfig.CycleTime);

                await orchestrator.StartAsync();

                // Wait for cancellation
                try
                {
                    await Task.Delay(Timeout.Infinite, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    // Normal shutdown
                }

                logger.Information(""Shutting down..."");
                await orchestrator.StopAsync();

                return 0;
            }
            catch (Exception ex)
            {
                logger.Fatal(ex, ""Fatal error during runtime execution"");
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static ILogger CreateLogger(RuntimeConfig config)
        {
            var loggerConfig = new LoggerConfiguration()
                .MinimumLevel.Is(config.LogLevel)
                .WriteTo.Console();

            if (!string.IsNullOrEmpty(config.LogFile))
            {
                loggerConfig.WriteTo.File(config.LogFile);
            }

            return loggerConfig.CreateLogger();
        }

        private static IRuleCoordinator LoadRuleCoordinator(RuntimeConfig config, ILogger logger, RingBufferManager bufferManager)
        {
            return new RuleCoordinator(logger, bufferManager);
        }
    }
}"
            );

            return new GeneratedFileInfo
            {
                FileName = "Program.cs",
                FilePath = Path.Combine("Generated", "Program.cs"),
                Content = builder.ToString(),
                Namespace = "Pulsar.Runtime.Rules",
            };
        }

        private static GeneratedFileInfo GenerateEmbeddedConfig(BuildConfig config)
        {
            var builder = new StringBuilder();
            builder.AppendLine(GenerateFileHeader());
            builder.AppendLine(
                @"
namespace Pulsar.Runtime.Rules
{
    internal static class EmbeddedConfig
    {"
            );

            builder.AppendLine(
                $"        public static readonly string RedisConnectionString = \"{config.RedisConnection}\";"
            );
            builder.AppendLine(
                $"        public static readonly int CycleTime = {config.CycleTime};"
            );
            builder.AppendLine(
                $"        public static readonly int BufferCapacity = {config.BufferCapacity};"
            );
            builder.AppendLine(
                $"        public static readonly string[] ValidSensors = new string[] {{"
            );
            foreach (var sensor in config.AdditionalUsings)
            {
                builder.AppendLine($"            \"{sensor}\",");
            }
            builder.AppendLine("        };");
            builder.AppendLine(
                @"    }
}"
            );

            return new GeneratedFileInfo
            {
                FileName = "EmbeddedConfig.cs",
                FilePath = Path.Combine("Generated", "EmbeddedConfig.cs"),
                Content = builder.ToString(),
                Namespace = "Pulsar.Runtime.Rules",
            };
        }

        private static GeneratedFileInfo GenerateRuntimeConfig()
        {
            var builder = new StringBuilder();
            builder.AppendLine(GenerateFileHeader());
            builder.AppendLine(
                @"
using Serilog.Events;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pulsar.Runtime.Rules
{
    public class RuntimeConfig
    {
        private string _redisConnectionString = ""localhost:6379"";

        [JsonPropertyName(""RedisConnectionString"")]
        public string RedisConnectionString
        {
            get => _redisConnectionString;
            set => _redisConnectionString = string.IsNullOrEmpty(value) ? ""localhost:6379"" : value;
        }

        [JsonPropertyName(""CycleTime"")]
        [JsonConverter(typeof(TimeSpanConverter))]
        public TimeSpan? CycleTime { get; set; }

        [JsonPropertyName(""LogLevel"")]
        public LogEventLevel LogLevel { get; set; } = LogEventLevel.Information;

        [JsonPropertyName(""BufferCapacity"")]
        public int BufferCapacity { get; set; } = 100;

        [JsonPropertyName(""LogFile"")]
        public string? LogFile { get; set; }

        [JsonPropertyName(""RequiredSensors"")]
        public string[] RequiredSensors { get; set; } = Array.Empty<string>();
    }
}"
            );

            return new GeneratedFileInfo
            {
                FileName = "RuntimeConfig.cs",
                FilePath = Path.Combine("Generated", "RuntimeConfig.cs"),
                Content = builder.ToString(),
                Namespace = "Pulsar.Runtime.Rules",
            };
        }

        private static List<GeneratedFileInfo> GenerateRuleFiles(
            List<RuleDefinition> rules,
            BuildConfig config
        )
        {
            var files = new List<GeneratedFileInfo>();

            // Get layer assignments for rules
            var layerMap = AssignLayers(rules);
            var rulesByLayer = GetRulesByLayer(rules, layerMap);

            // Generate interface file
            files.Add(GenerateInterfaceFile());

            // Generate rule group implementations
            var groups = SplitRulesIntoGroups(
                rules,
                layerMap,
                new RuleGroupingConfig
                {
                    MaxRulesPerFile = config.MaxRulesPerFile,
                    MaxLinesPerFile = config.MaxLinesPerFile,
                    GroupParallelRules = config.GroupParallelRules,
                }
            );

            foreach (var group in groups)
            {
                files.Add(GenerateGroupImplementation(group.Key, group.Value, layerMap));
            }

            // Generate coordinator
            files.Add(GenerateRuleCoordinator(groups, layerMap));

            // Generate metadata
            files.Add(GenerateMetadataFile(rules, layerMap));

            files.Add(GenerateRuntimeProjectFile(config));

            files = ApplyPostGenerationFixups(files);

            return files;
        }

        private static GeneratedFileInfo GenerateRuntimeProjectFile(BuildConfig config)
        {
            var builder = new StringBuilder();
            builder.AppendLine("<Project Sdk=\"Microsoft.NET.Sdk\">");
            builder.AppendLine("  <PropertyGroup>");
            builder.AppendLine("    <OutputType>Exe</OutputType>");
            builder.AppendLine("    <TargetFramework>net9.0</TargetFramework>");
            builder.AppendLine("    <Nullable>enable</Nullable>");
            builder.AppendLine("  </PropertyGroup>");
            builder.AppendLine("  <ItemGroup>");
            builder.AppendLine("    <PackageReference Include=\"Serilog\" Version=\"4.0.0\" />");
            builder.AppendLine(
                "    <PackageReference Include=\"Prometheus.Client\" Version=\"4.1.0\" />"
            );
            builder.AppendLine(
                "    <PackageReference Include=\"StackExchange.Redis\" Version=\"2.8.16\" />"
            );
            builder.AppendLine("  </ItemGroup>");
            builder.AppendLine("  <ItemGroup>");
            builder.AppendLine(
                "    <ProjectReference Include=\"../../Pulsar.Runtime/Pulsar.Runtime.csproj\" />"
            );
            builder.AppendLine("  </ItemGroup>");
            builder.AppendLine("</Project>");

            return new GeneratedFileInfo
            {
                FileName = "Runtime.csproj",
                FilePath = "Runtime.csproj",
                Content = builder.ToString(),
                Namespace = "",
            };
        }

        private static List<GeneratedFileInfo> ApplyPostGenerationFixups(
            List<GeneratedFileInfo> files
        )
        {
            foreach (var file in files)
            {
                // Replace 'using Pulsar.Runtime.Common;' with 'using Pulsar.Runtime;'
                file.Content = file.Content.Replace(
                    "using Pulsar.Runtime.Common;",
                    "using Pulsar.Runtime;"
                );

                // For RuntimeConfig.cs, ensure it has 'using System;' at the top
                if (file.FileName.Equals("RuntimeConfig.cs", StringComparison.OrdinalIgnoreCase))
                {
                    if (!file.Content.Contains("using System;"))
                    {
                        file.Content = "using System;" + Environment.NewLine + file.Content;
                    }
                }
            }
            return files;
        }

        private static GeneratedFileInfo GenerateConfigurationLoader()
        {
            var builder = new StringBuilder();
            builder.AppendLine(GenerateFileHeader());
            builder.AppendLine(
                @"
using System;
using System.Text.Json;
using System.IO;
using Serilog;

namespace Pulsar.Runtime.Rules
{
    internal static class ConfigurationLoader
    {
        internal static RuntimeConfig LoadConfiguration(string[] args, bool requireSensors = true, string? configPath = null)
        {
            var config = new RuntimeConfig();

            if (configPath != null && File.Exists(configPath))
            {
                var jsonContent = File.ReadAllText(configPath);
                config = JsonSerializer.Deserialize<RuntimeConfig>(jsonContent) ?? new RuntimeConfig();
            }

            return config;
        }
    }
}"
            );

            return new GeneratedFileInfo
            {
                FileName = "ConfigurationLoader.cs",
                FilePath = Path.Combine("Generated", "ConfigurationLoader.cs"),
                Content = builder.ToString(),
                Namespace = "Pulsar.Runtime.Rules",
            };
        }

        private static GeneratedFileInfo GenerateRedisService()
        {
            var builder = new StringBuilder();
            builder.AppendLine(GenerateFileHeader());
            builder.AppendLine(
                @"
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using StackExchange.Redis;
using Pulsar.Runtime.Rules;

namespace Pulsar.Runtime.Rules
{
    public class RedisService : IRedisService, IDisposable
    {
        private readonly ConnectionMultiplexer _redis;
        private readonly IDatabase _db;
        private readonly ILogger _logger;
        private readonly SemaphoreSlim _connectionLock = new(1, 1);
        private readonly ConcurrentDictionary<string, DateTime> _lastErrorTime = new();
        private readonly TimeSpan _errorThrottleWindow = TimeSpan.FromSeconds(60);

        public RedisService(string connectionString, ILogger logger)
        {
            _logger = logger;

            try
            {
                var options = ConfigurationOptions.Parse(connectionString);
                options.AbortOnConnectFail = false;
                _redis = ConnectionMultiplexer.Connect(options);
                _db = _redis.GetDatabase();

                _redis.ConnectionFailed += (sender, e) =>
                    _logger.Error(""Redis connection failed: {@Error}"", e.Exception);
                _redis.ConnectionRestored += (sender, e) =>
                    _logger.Information(""Redis connection restored"");

                _logger.Information(""Redis connection initialized"");
            }
            catch (Exception ex)
            {
                _logger.Fatal(ex, ""Failed to initialize Redis connection"");
                throw;
            }
        }

        public async Task<Dictionary<string, (double Value, DateTime Timestamp)>> GetSensorValuesAsync(IEnumerable<string> sensorKeys)
        {
            var result = new Dictionary<string, (double Value, DateTime Timestamp)>();
            var keyArray = sensorKeys.ToArray();

            try
            {
                await _connectionLock.WaitAsync();

                var batch = _db.CreateBatch();
                var tasks = new List<Task<HashEntry[]>>();

                foreach (var key in keyArray)
                {
                    tasks.Add(batch.HashGetAllAsync(key));
                }

                batch.Execute();
                await Task.WhenAll(tasks);

                for (int i = 0; i < keyArray.Length; i++)
                {
                    var hashValues = tasks[i].Result;

                    var valueEntry = hashValues.FirstOrDefault(he => he.Name == ""value"");
                    var timestampEntry = hashValues.FirstOrDefault(he => he.Name == ""timestamp"");

                    if (valueEntry.Value.HasValue && timestampEntry.Value.HasValue)
                    {
                        if (double.TryParse(valueEntry.Value.ToString(), out double value) &&
                            long.TryParse(timestampEntry.Value.ToString(), out long ticksValue))
                        {
                            DateTime timestamp = new DateTime(ticksValue, DateTimeKind.Utc);
                            result[keyArray[i]] = (value, timestamp);
                        }
                        else
                        {
                            LogThrottledWarning($""Invalid value or timestamp format for sensor {keyArray[i]}"");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, ""Error fetching sensor values from Redis"");
                throw;
            }
            finally
            {
                _connectionLock.Release();
            }

            return result;
        }

        public async Task SetOutputValuesAsync(Dictionary<string, double> outputs)
        {
            if (!outputs.Any()) return;

            try
            {
                await _connectionLock.WaitAsync();

                var batch = _db.CreateBatch();
                var tasks = new List<Task>();
                var timestamp = DateTime.UtcNow.Ticks;

                foreach (var kvp in outputs)
                {
                    tasks.Add(batch.HashSetAsync(kvp.Key, new HashEntry[] {
                        new HashEntry(""value"", kvp.Value.ToString(""G17"")),
                        new HashEntry(""timestamp"", timestamp)
                    }));
                }

                batch.Execute();
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, ""Error writing output values to Redis"");
                throw;
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        private void LogThrottledWarning(string message)
        {
            if (_lastErrorTime.TryGetValue(message, out var lastTime))
            {
                if (DateTime.UtcNow - lastTime < _errorThrottleWindow)
                {
                    return;
                }
            }

            _lastErrorTime.AddOrUpdate(message, DateTime.UtcNow, (_, _) => DateTime.UtcNow);
            _logger.Warning(message);
        }

        public void Dispose()
        {
            _connectionLock.Dispose();
            _redis?.Dispose();
        }
    }
}"
            );

            return new GeneratedFileInfo
            {
                FileName = "RedisService.cs",
                FilePath = Path.Combine("Generated", "RedisService.cs"),
                Content = builder.ToString(),
                Namespace = "Pulsar.Runtime.Rules",
            };
        }
    }
}
