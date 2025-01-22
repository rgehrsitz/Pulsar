// File: Pulsar.Compiler/CodeGenerator.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
            RuleGroupingConfig? config = null)
        {
            // Use default configuration if not provided
            config ??= new RuleGroupingConfig();

            if (rules == null || !rules.Any())
            {
                return new List<GeneratedFileInfo>
            {
                GenerateEmptyCompiledRules()
            };
            }

            var files = new List<GeneratedFileInfo>();

            // Add interface file
            files.Add(GenerateInterfaceFile());

            // Analyze dependencies and create layer mapping
            var layerMap = AssignLayers(rules);
            var rulesByLayer = GetRulesByLayer(rules, layerMap);

            // Split rules into groups based on configuration
            var groups = SplitRulesIntoGroups(rules, layerMap, config);

            // Generate group implementation files
            foreach (var (groupIndex, groupRules) in groups)
            {
                var groupFile = GenerateGroupImplementation(groupIndex, groupRules, layerMap);

                // Add source tracking comments for rules in this group
                AddSourceTrackingComments(groupFile, groupRules);

                files.Add(groupFile);
            }

            // Generate coordinator to manage groups
            files.Add(GenerateRuleCoordinator(groups, layerMap));

            // Generate metadata file with source tracking
            files.Add(GenerateMetadataFile(rules, layerMap));

            return files;
        }

        /// <summary>
        /// Adds source tracking comments to the generated file content
        /// </summary>
        private static void AddSourceTrackingComments(
            GeneratedFileInfo fileInfo,
            List<RuleDefinition> groupRules)
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
                sourceComment += string.Join("\n",
                    groupRules.Select(r => $"//   - {r.Name} (from {r.SourceInfo?.FileName ?? "unknown"})")
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

        private static GeneratedFileInfo GenerateCompiledRulesClass(Dictionary<int, List<RuleDefinition>> rulesByLayer)
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
            builder.AppendLine("        public CompiledRules(ILogger logger, RingBufferManager bufferManager)");
            builder.AppendLine("        {");
            builder.AppendLine("            _logger = logger ?? throw new ArgumentNullException(nameof(logger));");
            builder.AppendLine("            _bufferManager = bufferManager ?? throw new ArgumentNullException(nameof(bufferManager));");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        public void Evaluate(Dictionary<string, double> inputs, Dictionary<string, double> outputs, RingBufferManager bufferManager)");
            builder.AppendLine("        {");
            builder.AppendLine("            try");
            builder.AppendLine("            {");
            builder.AppendLine("                _logger.Debug(\"Starting rule evaluation\");");

            // Call each layer's evaluation method in order
            foreach (var layer in rulesByLayer.Keys.OrderBy(k => k))
            {
                builder.AppendLine($"                EvaluateLayer{layer}(inputs, outputs, bufferManager);");
            }

            builder.AppendLine("                _logger.Debug(\"Rule evaluation completed\");");
            builder.AppendLine("            }");
            builder.AppendLine("            catch (Exception ex)");
            builder.AppendLine("            {");
            builder.AppendLine("                _logger.Error(ex, \"Error during rule evaluation\");");
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
                    End = rulesByLayer.Keys.Any() ? rulesByLayer.Keys.Max() : 0
                }
            };
        }

        private static GeneratedFileInfo GenerateLayerImplementation(int layer, List<RuleDefinition> rules)
        {
            var builder = new StringBuilder();
            builder.AppendLine(GenerateFileHeader());
            builder.AppendLine(GenerateCommonUsings());
            builder.AppendLine("namespace Pulsar.Runtime.Rules");
            builder.AppendLine("{");
            builder.AppendLine("    public partial class CompiledRules");
            builder.AppendLine("    {");
            builder.AppendLine($"        private void EvaluateLayer{layer}(Dictionary<string, double> inputs, Dictionary<string, double> outputs, RingBufferManager bufferManager)");
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
                    builder.AppendLine($"                // Source: {rule.SourceInfo.FileName}:{rule.SourceInfo.LineNumber}");
                }
                builder.AppendLine($"                // Rule: {rule.Name}");
                if (!string.IsNullOrEmpty(rule.Description))
                {
                    builder.AppendLine($"                // Description: {rule.Description}");
                }

                // Add debug logging
                builder.AppendLine($"                _logger.Debug(\"Evaluating rule {rule.Name}\");");

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
                    GenerateActions(builder, rule.Actions, "                ");  // Uses default indent
                }
            }

            builder.AppendLine("            }");
            builder.AppendLine("            catch (Exception ex)");
            builder.AppendLine("            {");
            builder.AppendLine($"                _logger.Error(ex, \"Error evaluating layer {layer}\");");
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
                LayerRange = new RuleLayerRange { Start = layer, End = layer }
            };
        }

        private static void GenerateActions(StringBuilder builder, List<ActionDefinition> actions, string indent = "            ")
        {
            foreach (var action in actions.OfType<SetValueAction>())
            {
                string valueAssignment;
                if (action.Value.HasValue)
                {
                    valueAssignment = action.Value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
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
                "Math.Abs", "Math.Pow", "Math.Sqrt", "Math.Sin", "Math.Cos", "Math.Tan",
                "Math.Log", "Math.Exp", "Math.Floor", "Math.Ceiling", "Math.Round"
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
            builder.AppendLine("        public CompiledRules(ILogger logger, RingBufferManager bufferManager)");
            builder.AppendLine("        {");
            builder.AppendLine("            _logger = logger ?? throw new ArgumentNullException(nameof(logger));");
            builder.AppendLine("            _bufferManager = bufferManager ?? throw new ArgumentNullException(nameof(bufferManager));");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        public void Evaluate(Dictionary<string, double> inputs, Dictionary<string, double> outputs, RingBufferManager bufferManager)");
            builder.AppendLine("        {");
            builder.AppendLine("            _logger.Debug(\"No rules to evaluate\");");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine("}");

            return new GeneratedFileInfo
            {
                FileName = "CompiledRules.cs",
                Content = builder.ToString(),
                Namespace = "Pulsar.Runtime.Rules"
            };
        }

        private static string GenerateFileHeader()
        {
            return @"// Generated code - do not modify directly
// Generated at: " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC") + @"
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
            builder.AppendLine("        void Evaluate(Dictionary<string, double> inputs, Dictionary<string, double> outputs, RingBufferManager bufferManager);");
            builder.AppendLine("    }");
            builder.AppendLine("}");

            return new GeneratedFileInfo
            {
                FileName = "ICompiledRules.cs",
                Content = builder.ToString(),
                Namespace = "Pulsar.Runtime.Rules"
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

        private static Dictionary<string, HashSet<string>> BuildDependencyGraph(List<RuleDefinition> rules)
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
            HashSet<string> visiting)
        {
            if (visiting.Contains(ruleName))
            {
                throw new InvalidOperationException($"Cyclic dependency detected involving rule '{ruleName}'");
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

        private static List<string> GetDependencies(RuleDefinition rule, Dictionary<string, string> outputRules)
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
            Dictionary<string, int> layerMap)
        {
            return rules.GroupBy(r => layerMap[r.Name])
                       .ToDictionary(g => g.Key, g => g.ToList());
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
            builder.AppendLine($"        private void Evaluate_{rule.Name}(Dictionary<string, double> inputs, Dictionary<string, double> outputs, RingBufferManager bufferManager)");
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
                LayerRange = new RuleLayerRange { Start = layer, End = layer }
            };
        }

        private static GeneratedFileInfo GenerateMetadataFile(List<RuleDefinition> rules, Dictionary<string, int> layerMap)
        {
            var metadata = new RuleManifest
            {
                GeneratedAt = DateTime.UtcNow,
                SchemaVersion = "1.0"
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
                    OutputSensors = GetOutputSensors(rule)
                };
            }

            var json = System.Text.Json.JsonSerializer.Serialize(metadata, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });

            return new GeneratedFileInfo
            {
                FileName = "rules.manifest.json",
                Content = json,
                Namespace = "Pulsar.Runtime.Rules"
            };
        }

        private static string GenerateCondition(ConditionGroup conditions, bool isPartOfOr = false)
        {
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

        private static string GenerateConditionExpression(ConditionDefinition condition, bool isPartOfOr = false)
        {
            string expression = condition switch
            {
                ComparisonCondition comp => $"inputs[\"{comp.Sensor}\"] {GetOperator(comp.Operator)} {comp.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                ExpressionCondition expr => FixupExpression(expr.Expression),
                ThresholdOverTimeCondition threshold => $"_bufferManager.IsAboveThresholdForDuration(\"{threshold.Sensor}\", {threshold.Threshold}, TimeSpan.FromMilliseconds({threshold.Duration}))",
                ConditionGroup group => GenerateCondition(group, isPartOfOr),
                _ => throw new NotSupportedException($"Unsupported condition type: {condition.GetType().Name}")
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
            return rule.Actions.OfType<SetValueAction>()
                       .Select(a => a.Key)
                       .ToList();
        }

        private static Dictionary<int, List<RuleDefinition>> SplitRulesIntoGroups(
            List<RuleDefinition> rules,
            Dictionary<string, int> layerMap,
            RuleGroupingConfig config)
        {
            var groups = new Dictionary<int, List<RuleDefinition>>();
            var currentGroupIndex = 0;

            // Sort rules by layer to maintain dependency order
            var sortedRules = rules.OrderBy(r => layerMap[r.Name]).ToList();

            // Group rules by layer first
            var rulesByLayer = sortedRules.GroupBy(r => layerMap[r.Name])
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
                    if (currentRules.Count >= config.MaxRulesPerFile ||
                        (ruleLayer != currentLayer && currentRules.Any()))
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

            return groups;
        }

        private static GeneratedFileInfo GenerateGroupImplementation(
           int groupIndex,
           List<RuleDefinition> rules,
           Dictionary<string, int> layerMap)
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
            builder.AppendLine($"        public RuleGroup{groupIndex}(ILogger logger, RingBufferManager bufferManager)");
            builder.AppendLine("        {");
            builder.AppendLine("            _logger = logger ?? throw new ArgumentNullException(nameof(logger));");
            builder.AppendLine("            _bufferManager = bufferManager ?? throw new ArgumentNullException(nameof(bufferManager));");
            builder.AppendLine("        }");
            builder.AppendLine();

            // Interface implementation
            builder.AppendLine("        public void EvaluateGroup(Dictionary<string, double> inputs, Dictionary<string, double> outputs, RingBufferManager bufferManager)");
            builder.AppendLine("        {");
            builder.AppendLine("            try");
            builder.AppendLine("            {");
            builder.AppendLine($"                _logger.Debug(\"Evaluating rule group {groupIndex}\");");

            // Generate rule implementations in layer order
            foreach (var rule in rules.OrderBy(r => layerMap[r.Name]))
            {
                // Add source tracking
                if (rule.SourceInfo != null)
                {
                    builder.AppendLine($"                // Source: {rule.SourceInfo.FileName}:{rule.SourceInfo.LineNumber}");
                }
                builder.AppendLine($"                // Rule: {rule.Name}");
                if (!string.IsNullOrEmpty(rule.Description))
                {
                    builder.AppendLine($"                // Description: {rule.Description}");
                }

                builder.AppendLine($"                _logger.Debug(\"Evaluating rule {rule.Name}\");");

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
            builder.AppendLine($"                _logger.Error(ex, \"Error evaluating rule group {groupIndex}\");");
            builder.AppendLine("                throw;");
            builder.AppendLine("            }");
            builder.AppendLine($"            _logger.Debug(\"Completed rule group {groupIndex}\");");
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
                    End = rules.Max(r => layerMap[r.Name])
                }
            };
        }

        private static GeneratedFileInfo GenerateRuleCoordinator(
            Dictionary<int, List<RuleDefinition>> groups,
            Dictionary<string, int> layerMap)
        {
            var builder = new StringBuilder();
            builder.AppendLine(GenerateFileHeader());
            builder.AppendLine(GenerateCommonUsings());
            builder.AppendLine("namespace Pulsar.Runtime.Rules");
            builder.AppendLine("{");

            // Generate concrete coordinator class instead of partial
            builder.AppendLine("    public class RuleCoordinator : IRuleCoordinator");
            builder.AppendLine("    {");

            // Fields for logger and each rule group
            builder.AppendLine("        private readonly ILogger _logger;");
            builder.AppendLine("        private readonly RingBufferManager _bufferManager;");

            // Add fields for each group
            foreach (var groupIndex in groups.Keys)
            {
                builder.AppendLine($"        private readonly RuleGroup{groupIndex} _group{groupIndex};");
            }

            builder.AppendLine();

            // Constructor
            builder.AppendLine("        public RuleCoordinator(ILogger logger, RingBufferManager bufferManager)");
            builder.AppendLine("        {");
            builder.AppendLine("            _logger = logger ?? throw new ArgumentNullException(nameof(logger));");
            builder.AppendLine("            _bufferManager = bufferManager ?? throw new ArgumentNullException(nameof(bufferManager));");

            // Initialize each group
            foreach (var groupIndex in groups.Keys)
            {
                builder.AppendLine($"            _group{groupIndex} = new RuleGroup{groupIndex}(logger, bufferManager);");
            }
            builder.AppendLine("        }");
            builder.AppendLine();

            // Evaluate method
            builder.AppendLine("        public void Evaluate(Dictionary<string, double> inputs, Dictionary<string, double> outputs, RingBufferManager bufferManager)");
            builder.AppendLine("        {");
            builder.AppendLine("            try");
            builder.AppendLine("            {");
            builder.AppendLine("                _logger.Debug(\"Starting rule evaluation\");");

            // Call groups in order based on their layer ranges
            var orderedGroups = groups
                .OrderBy(g => g.Value.Min(r => layerMap[r.Name]))
                .Select(g => g.Key);

            foreach (var groupIndex in orderedGroups)
            {
                builder.AppendLine($"                _group{groupIndex}.EvaluateGroup(inputs, outputs, bufferManager);");
            }

            builder.AppendLine("                _logger.Debug(\"Completed rule evaluation\");");
            builder.AppendLine("            }");
            builder.AppendLine("            catch (Exception ex)");
            builder.AppendLine("            {");
            builder.AppendLine("                _logger.Error(ex, \"Error during rule evaluation\");");
            builder.AppendLine("                throw;");
            builder.AppendLine("            }");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine("}");

            return new GeneratedFileInfo
            {
                FileName = "RuleCoordinator.cs",
                Content = builder.ToString(),
                Namespace = "Pulsar.Runtime.Rules"
            };
        }
    }
}
