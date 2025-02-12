// File: Pulsar.Compiler/Generation/CodeGenerator.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Pulsar.Compiler.Config;
using Pulsar.Compiler.Models;
using Serilog;

namespace Pulsar.Compiler.Generation
{
    public class RuleGroupingConfig
    {
        public int MaxRulesPerFile { get; set; } = 100;
        public int MaxLinesPerFile { get; set; } = 1000;
        public bool GroupParallelRules { get; set; } = true;
    }

    public class CodeGenerator
    {
        private static readonly ILogger _logger = Log.ForContext<CodeGenerator>();

        public static List<Pulsar.Compiler.Models.GeneratedFileInfo> GenerateCSharp(
            List<RuleDefinition> rules,
            BuildConfig? config = null)
        {
            config ??= new BuildConfig
            {
                OutputPath = "Generated",
                Target = "win-x64",
                ProjectName = "Pulsar.Compiler",
                TargetFramework = "net9.0",
            };

            var files = new List<Pulsar.Compiler.Models.GeneratedFileInfo>();

            try
            {
                // Copy template files first
                var templateManager = new TemplateManager(new SerilogAdapter(_logger));
                files.AddRange(templateManager.CopyTemplateFiles(config.OutputPath));

                // Get layer assignments for rules
                var layerMap = AssignLayers(rules);
                var rulesByLayer = GetRulesByLayer(rules, layerMap);

                // Generate rule-specific files
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

                // Generate each rule group
                foreach (var group in groups)
                {
                    files.Add(GenerateGroupImplementation(group.Key, group.Value, layerMap));
                }

                // Generate coordinator
                files.Add(GenerateRuleCoordinator(groups, layerMap));

                // Generate embedded config
                files.Add(GenerateEmbeddedConfig(config));

                // Generate metadata
                files.Add(GenerateMetadataFile(rules, layerMap.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString())));

                files = ApplyPostGenerationFixups(files);

                return files;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error generating C# files");
                throw;
            }
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

            // Initialize graph and record outputs
            foreach (var rule in rules)
            {
                graph[rule.Name] = new HashSet<string>();
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

        private static Dictionary<int, List<RuleDefinition>> GetRulesByLayer(
            List<RuleDefinition> rules,
            Dictionary<string, int> layerMap
        )
        {
            return rules.GroupBy(r => layerMap[r.Name]).ToDictionary(g => g.Key, g => g.ToList());
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

            if (config.GroupParallelRules)
            {
                var rulesByLayer = sortedRules
                    .GroupBy(r => layerMap[r.Name])
                    .OrderBy(g => g.Key)
                    .ToList();

                foreach (var layer in rulesByLayer)
                {
                    var layerRules = layer.ToList();
                    var remainingRules = layerRules.Count;
                    var currentIndex = 0;

                    while (remainingRules > 0)
                    {
                        var rulesInThisGroup = Math.Min(remainingRules, config.MaxRulesPerFile);
                        groups[currentGroupIndex++] = layerRules
                            .Skip(currentIndex)
                            .Take(rulesInThisGroup)
                            .ToList();
                        currentIndex += rulesInThisGroup;
                        remainingRules -= rulesInThisGroup;
                    }
                }
            }
            else
            {
                var currentRules = new List<RuleDefinition>();
                var currentLayer = layerMap[sortedRules.First().Name];

                foreach (var rule in sortedRules)
                {
                    var ruleLayer = layerMap[rule.Name];

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

        // Continuing CodeGenerator.cs...

        private static Pulsar.Compiler.Models.GeneratedFileInfo GenerateGroupImplementation(
            int groupIndex,
            List<RuleDefinition> rules,
            Dictionary<string, int> layerMap)
        {
            var builder = new StringBuilder();
            builder.AppendLine(CodeGenerator.GenerateFileHeader());
            builder.AppendLine(GenerateCommonUsings());
            builder.AppendLine("namespace Pulsar.Runtime.Rules");
            builder.AppendLine("{");

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

            return new Pulsar.Compiler.Models.GeneratedFileInfo
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

        private static Pulsar.Compiler.Models.GeneratedFileInfo GenerateRuleCoordinator(
            Dictionary<int, List<RuleDefinition>> groups,
            Dictionary<string, int> layerMap)
        {
            var builder = new StringBuilder();
            builder.AppendLine(CodeGenerator.GenerateFileHeader());
            builder.AppendLine(GenerateCommonUsings());
            builder.AppendLine();
            builder.AppendLine("using System.Diagnostics.CodeAnalysis;");
            builder.AppendLine("using System.Diagnostics.Metrics;");
            builder.AppendLine();
            builder.AppendLine("namespace Pulsar.Runtime.Rules");
            builder.AppendLine("{");

            // Generate coordinator class with AOT compatibility
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

            // Fields
            builder.AppendLine("        private readonly ILogger _logger;");
            builder.AppendLine("        private readonly RingBufferManager _bufferManager;");

            // Add fields for each group
            foreach (var group in groups.OrderBy(g => g.Key))
            {
                builder.AppendLine();
                builder.AppendLine($"        // Rules for Group {group.Key}:");
                foreach (var rule in group.Value)
                {
                    if (rule != null)
                    {
                        builder.AppendLine($"        // - {rule.Name}");
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

            // Initialize each group
            foreach (var groupIndex in groups.Keys.OrderBy(k => k))
            {
                builder.AppendLine("            try");
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

            // EvaluateRules implementation
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

            // Call groups in dependency order
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

                builder.AppendLine("                try");
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

            // Record metrics
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
            builder.AppendLine("    }");
            builder.AppendLine("}");

            return new Pulsar.Compiler.Models.GeneratedFileInfo
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

        private static Pulsar.Compiler.Models.GeneratedFileInfo GenerateMetadataFile(
            List<RuleDefinition> rules,
            Dictionary<string, string> layerMap)
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
                    Layer = int.Parse(layerMap[rule.Name].ToString()),
                    Description = rule.Description,
                    Dependencies = GetDependencies(rule, rules.ToDictionary(
                        r => r.Name,
                        r => string.Join(",", r.Actions.OfType<SetValueAction>().Select(a => a.Key))
                    )),
                    InputSensors = GetInputSensors(rule),
                    OutputSensors = GetOutputSensors(rule),
                    UsesTemporalConditions = HasTemporalConditions(rule),
                };
            }

            var json = System.Text.Json.JsonSerializer.Serialize(
                metadata,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }
            );

            return new Pulsar.Compiler.Models.GeneratedFileInfo
            {
                FileName = "rules.manifest.json",
                Content = json,
                Namespace = "Pulsar.Runtime.Rules",
            };
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
            builder.AppendLine("using Pulsar.Runtime.Rules;");
            return builder.ToString();
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
                else if (condition is ExpressionCondition expr)
                {
                    // Extract sensors from expression using regex
                    ExtractSensorsFromExpression(expr.Expression, sensors);
                }
                else if (condition is ThresholdOverTimeCondition temporal)
                {
                    sensors.Add(temporal.Sensor);
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

        private static bool HasTemporalConditions(RuleDefinition rule)
        {
            return rule.Conditions?.All?.Any(c => c is ThresholdOverTimeCondition) == true
                || rule.Conditions?.Any?.Any(c => c is ThresholdOverTimeCondition) == true;
        }

        private static void ExtractSensorsFromExpression(string expression, HashSet<string> sensors)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return;

            var sensorPattern = @"\b([a-zA-Z_][a-zA-Z0-9_]*)\b";
            var matches = Regex.Matches(expression, sensorPattern);

            foreach (Match match in matches)
            {
                var potentialSensor = match.Value;
                if (!IsMathFunction(potentialSensor))
                {
                    sensors.Add(potentialSensor);
                }
            }
        }

        private static bool IsMathFunction(string token)
        {
            string[] mathFunctions = {
                "Math",
                "Abs",
                "Max",
                "Min",
                "Round",
                "Floor",
                "Ceiling",
                "Sqrt",
                "Sin",
                "Cos",
                "Tan",
            };
            return mathFunctions.Contains(token);
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

        private static string FixupExpression(string expression)
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

        private static Pulsar.Compiler.Models.GeneratedFileInfo GenerateEmbeddedConfig(Pulsar.Compiler.Config.BuildConfig config)
        {
            string content = "// Embedded config for Pulsar Compiler" + Environment.NewLine;
            return new Pulsar.Compiler.Models.GeneratedFileInfo
            {
                FileName = System.IO.Path.Combine(config.OutputPath, "EmbeddedConfig.cs"),
                Content = content
            };
        }

        private static List<Pulsar.Compiler.Models.GeneratedFileInfo> ApplyPostGenerationFixups(List<Pulsar.Compiler.Models.GeneratedFileInfo> files)
        {
            // Currently no post-generation fixups needed.
            return files;
        }

        private static string GenerateFileHeader()
        {
            return "// Generated by Pulsar AOT Compiler";
        }

        public class SerilogAdapter : Microsoft.Extensions.Logging.ILogger
        {
            private readonly Serilog.ILogger _logger;
            public SerilogAdapter(Serilog.ILogger logger)
            {
                _logger = logger;
            }

            public IDisposable BeginScope<TState>(TState state)
            {
                return new LogScope();
            }

            public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

            public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId,
                TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                _logger.Information(formatter(state, exception));
            }

            private class LogScope : IDisposable
            {
                public void Dispose() { }
            }
        }
    }
}