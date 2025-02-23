// File: Pulsar.Compiler/Generation/CodeGenerator.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Pulsar.Compiler.Config;
using Pulsar.Compiler.Models;
using Pulsar.Compiler.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Pulsar.Compiler.Generation
{
    public class RuleGroupingConfig
    {
        public int MaxRulesPerFile { get; set; } = 100;
        public int MaxLinesPerFile { get; set; } = 1000;
        public bool GroupParallelRules { get; set; } = true;
    }

    public class CodeGenerator : IDisposable
    {
        private readonly ILogger<CodeGenerator> _logger;

        public CodeGenerator(ILogger<CodeGenerator>? logger = null)
        {
            _logger = logger ?? NullLogger<CodeGenerator>.Instance;
        }

        public void Dispose()
        {
            // No unmanaged resources to dispose
            GC.SuppressFinalize(this);
        }

        public List<Pulsar.Compiler.Models.GeneratedFileInfo> GenerateCSharp(
            List<RuleDefinition> rules,
            BuildConfig buildConfig)
        {
            if (buildConfig == null)
            {
                throw new ArgumentNullException(nameof(buildConfig));
            }

            var analyzer = new DependencyAnalyzer();
            var layerMap = analyzer.GetDependencyMap(rules);
            var ruleGroups = SplitRulesIntoGroups(rules, layerMap);
            var generatedFiles = new List<Pulsar.Compiler.Models.GeneratedFileInfo>();

            // Generate rule groups
            for (int i = 0; i < ruleGroups.Count; i++)
            {
                var groupImplementation = GenerateGroupImplementation(i, ruleGroups[i], layerMap);
                groupImplementation.Namespace = buildConfig.Namespace;
                generatedFiles.Add(groupImplementation);
            }

            // Generate rule coordinator
            var coordinator = GenerateRuleCoordinator(ruleGroups, layerMap);
            coordinator.Namespace = buildConfig.Namespace;
            generatedFiles.Add(coordinator);

            // Generate metadata file
            var metadata = GenerateMetadataFile(rules, layerMap);
            metadata.Namespace = buildConfig.Namespace;
            generatedFiles.Add(metadata);

            // Generate embedded config
            var embeddedConfig = GenerateEmbeddedConfig(buildConfig);
            embeddedConfig.Namespace = buildConfig.Namespace;
            generatedFiles.Add(embeddedConfig);

            return generatedFiles;
        }

        public Dictionary<string, int> AssignLayers(List<RuleDefinition> rules)
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

        private Dictionary<string, HashSet<string>> BuildDependencyGraph(List<RuleDefinition> rules)
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

        private List<string> GetDependencies(RuleDefinition rule, Dictionary<string, string> outputRules)
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

        private void AssignLayerDFS(
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

        private Dictionary<int, List<RuleDefinition>> GetRulesByLayer(
            List<RuleDefinition> rules,
            Dictionary<string, string> layerMap
        )
        {
            var rulesByLayer = new Dictionary<int, List<RuleDefinition>>();

            foreach (var rule in rules)
            {
                var layer = int.Parse(layerMap[rule.Name]);
                if (!rulesByLayer.ContainsKey(layer))
                {
                    rulesByLayer[layer] = new List<RuleDefinition>();
                }
                rulesByLayer[layer].Add(rule);
            }

            return rulesByLayer;
        }

        public GeneratedFileInfo GenerateGroupImplementation(
            int groupId,
            List<RuleDefinition> rules,
            Dictionary<string, string> layerMap)
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
            sb.AppendLine("using Pulsar.Runtime.Buffers;");
            sb.AppendLine("using Pulsar.Runtime.Rules;");
            sb.AppendLine("using Pulsar.Runtime.Interfaces;");
            sb.AppendLine();

            sb.AppendLine("namespace Beacon.Runtime.Generated");
            sb.AppendLine("{");

            // Class declaration
            sb.AppendLine($"    public class RuleGroup{groupId} : TemplateRuleGroup");
            sb.AppendLine("    {");

            // Constructor
            sb.AppendLine($"        public RuleGroup{groupId}(");
            sb.AppendLine("            IRedisService redis,");
            sb.AppendLine("            ILogger logger,");
            sb.AppendLine("            RingBufferManager bufferManager)");
            sb.AppendLine("            : base(redis, logger, bufferManager)");
            sb.AppendLine("        {");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Required sensors property
            var requiredSensors = rules.SelectMany(r => GetInputSensors(r)).Distinct().ToList();
            sb.AppendLine("        public override string[] RequiredSensors => new[]");
            sb.AppendLine("        {");
            foreach (var sensor in requiredSensors)
            {
                sb.AppendLine($"            \"{sensor}\",");
            }
            sb.AppendLine("        };");
            sb.AppendLine();

            // Rule evaluation method
            sb.AppendLine("        protected override async Task EvaluateRulesAsync(");
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
                    sb.AppendLine($"            if ({GenerateCondition(rule.Conditions)})");
                    sb.AppendLine("            {");

                    // Generate actions
                    if (rule.Actions != null)
                    {
                        foreach (var action in rule.Actions)
                        {
                            sb.AppendLine($"                {GenerateAction(action)}");
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
                            sb.AppendLine($"            {GenerateAction(action)}");
                        }
                    }
                }

                sb.AppendLine();
            }

            sb.AppendLine("            await Task.CompletedTask;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return new Pulsar.Compiler.Models.GeneratedFileInfo
            {
                FileName = $"RuleGroup{groupId}.cs",
                Content = sb.ToString(),
                Namespace = "Beacon.Runtime.Generated"
            };
        }

        private string GenerateCondition(ConditionGroup? conditions)
        {
            if (conditions == null)
            {
                return "true";
            }

            var parts = new List<string>();

            if (conditions.All?.Any() == true)
            {
                var allConditions = conditions.All.Select(GenerateConditionExpression);
                parts.Add($"({string.Join(" && ", allConditions)})");
            }

            if (conditions.Any?.Any() == true)
            {
                var anyConditions = conditions.Any.Select(GenerateConditionExpression);
                parts.Add($"({string.Join(" || ", anyConditions)})");
            }

            return parts.Count > 0 ? string.Join(" && ", parts) : "true";
        }

        private string GenerateConditionExpression(ConditionDefinition condition)
        {
            return condition switch
            {
                ComparisonCondition comparison => GenerateComparisonCondition(comparison),
                ExpressionCondition expression => FixupExpression(expression.Expression),
                ThresholdOverTimeCondition threshold => GenerateThresholdCondition(threshold),
                _ => throw new InvalidOperationException($"Unknown condition type: {condition.GetType().Name}")
            };
        }

        private string GenerateComparisonCondition(ComparisonCondition comparison)
        {
            var op = comparison.Operator switch
            {
                ComparisonOperator.GreaterThan => ">",
                ComparisonOperator.LessThan => "<",
                ComparisonOperator.GreaterThanOrEqual => ">=",
                ComparisonOperator.LessThanOrEqual => "<=",
                ComparisonOperator.EqualTo => "==",
                ComparisonOperator.NotEqualTo => "!=",
                _ => throw new InvalidOperationException($"Unknown operator: {comparison.Operator}")
            };

            return $"Convert.ToDouble(inputs[\"{comparison.Sensor}\"]) {op} {comparison.Value}";
        }

        private string GenerateThresholdCondition(ThresholdOverTimeCondition threshold)
        {
            return $"CheckThreshold(\"{threshold.Sensor}\", {threshold.Threshold}, {threshold.Duration}, \"{threshold.ComparisonOperator}\")";
        }

        private string GenerateAction(ActionDefinition action)
        {
            return action switch
            {
                SetValueAction setValue => GenerateSetValueAction(setValue),
                _ => throw new InvalidOperationException($"Unknown action type: {action.GetType().Name}")
            };
        }

        private string GenerateSetValueAction(SetValueAction setValue)
        {
            var value = !string.IsNullOrEmpty(setValue.ValueExpression)
                ? FixupExpression(setValue.ValueExpression)
                : setValue.Value.ToString();

            return $"outputs[\"{setValue.Key}\"] = {value};";
        }

        private string FixupExpression(string expression)
        {
            // Replace sensor references with dictionary lookups
            return Regex.Replace(
                expression,
                @"\$([a-zA-Z_][a-zA-Z0-9_]*)",
                m => $"Convert.ToDouble(inputs[\"{m.Groups[1].Value}\"])"
            );
        }

        public GeneratedFileInfo GenerateRuleCoordinator(
            List<List<RuleDefinition>> ruleGroups,
            Dictionary<string, string> layerMap)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// Auto-generated rule coordinator");
            sb.AppendLine("// Generated: " + DateTime.UtcNow.ToString("O"));
            sb.AppendLine();

            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine("using Microsoft.Extensions.Logging;");
            sb.AppendLine("using Prometheus;");
            sb.AppendLine("using Serilog;");
            sb.AppendLine("using Pulsar.Runtime.Buffers;");
            sb.AppendLine("using Pulsar.Runtime.Services;");
            sb.AppendLine("using Pulsar.Runtime.Rules;");
            sb.AppendLine("using Pulsar.Runtime.Interfaces;");
            sb.AppendLine();

            sb.AppendLine("namespace Beacon.Runtime.Generated");
            sb.AppendLine("{");
            sb.AppendLine("    public class RuleCoordinator : IRuleCoordinator");
            sb.AppendLine("    {");
            sb.AppendLine("        private readonly IRedisService _redis;");
            sb.AppendLine("        private readonly ILogger _logger;");
            sb.AppendLine("        private readonly RingBufferManager _bufferManager;");
            sb.AppendLine("        private readonly List<TemplateRuleGroup> _ruleGroups;");
            sb.AppendLine();

            // Add Prometheus metrics
            sb.AppendLine("        private static readonly Counter RuleEvaluationsTotal = Metrics");
            sb.AppendLine("            .CreateCounter(\"pulsar_rule_evaluations_total\", \"Total number of rule evaluations\");");
            sb.AppendLine();
            sb.AppendLine("        private static readonly Histogram RuleEvaluationDuration = Metrics");
            sb.AppendLine("            .CreateHistogram(\"pulsar_rule_evaluation_duration_seconds\", \"Duration of rule evaluations\");");
            sb.AppendLine();

            // Constructor
            sb.AppendLine("        public RuleCoordinator(IRedisService redis, ILogger logger, RingBufferManager bufferManager)");
            sb.AppendLine("        {");
            sb.AppendLine("            _redis = redis ?? throw new ArgumentNullException(nameof(redis));");
            sb.AppendLine("            _logger = logger ?? throw new ArgumentNullException(nameof(logger));");
            sb.AppendLine("            _bufferManager = bufferManager ?? throw new ArgumentNullException(nameof(bufferManager));");
            sb.AppendLine("            _ruleGroups = new List<TemplateRuleGroup>();");
            sb.AppendLine();

            // Initialize rule groups
            for (int i = 0; i < ruleGroups.Count; i++)
            {
                sb.AppendLine($"            _ruleGroups.Add(new RuleGroup{i}(_redis, _logger, _bufferManager));");
            }

            sb.AppendLine("        }");
            sb.AppendLine();

            // EvaluateAllRulesAsync method
            sb.AppendLine("        public async Task EvaluateAllRulesAsync()");
            sb.AppendLine("        {");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine("                using var timer = RuleEvaluationDuration.NewTimer();");
            sb.AppendLine("                var inputs = await _redis.GetAllInputsAsync();");
            sb.AppendLine("                var outputs = new Dictionary<string, object>();");
            sb.AppendLine();

            // Evaluate each rule group in sequence
            for (int i = 0; i < ruleGroups.Count; i++)
            {
                sb.AppendLine($"                _logger.LogDebug(\"Evaluating rule group {i}\");");
                sb.AppendLine($"                await _ruleGroups[{i}].EvaluateRulesAsync(inputs, outputs);");
                sb.AppendLine($"                RuleEvaluationsTotal.Inc();");
            }

            sb.AppendLine();
            sb.AppendLine("                await _redis.SetOutputsAsync(outputs);");
            sb.AppendLine("            }");
            sb.AppendLine("            catch (Exception ex)");
            sb.AppendLine("            {");
            sb.AppendLine("                _logger.LogError(ex, \"Error evaluating rules\");");
            sb.AppendLine("                throw;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return new Pulsar.Compiler.Models.GeneratedFileInfo
            {
                FileName = "RuleCoordinator.cs",
                Content = sb.ToString(),
                Namespace = "Beacon.Runtime.Generated"
            };
        }

        public GeneratedFileInfo GenerateMetadataFile(List<RuleDefinition> rules, Dictionary<string, string> layerMap)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// Auto-generated metadata file");
            sb.AppendLine("// Generated: " + DateTime.UtcNow.ToString("O"));
            sb.AppendLine();

            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine();

            sb.AppendLine("namespace Beacon.Runtime.Generated");
            sb.AppendLine("{");
            sb.AppendLine("    public static class RuleMetadata");
            sb.AppendLine("    {");
            
            // Rule information
            sb.AppendLine("        public static readonly Dictionary<string, RuleInfo> Rules = new()");
            sb.AppendLine("        {");
            foreach (var rule in rules)
            {
                sb.AppendLine($"            [\"{rule.Name}\"] = new RuleInfo");
                sb.AppendLine("            {");
                sb.AppendLine($"                Name = \"{rule.Name}\",");
                sb.AppendLine($"                Description = \"{rule.Description}\",");
                sb.AppendLine($"                Layer = {layerMap[rule.Name]},");
                sb.AppendLine($"                SourceFile = \"{rule.SourceFile}\",");
                sb.AppendLine($"                LineNumber = {rule.LineNumber},");
                sb.AppendLine("                InputSensors = new[] { " + string.Join(", ", GetInputSensors(rule).Select(s => $"\"{s}\"")) + " },");
                sb.AppendLine("                OutputSensors = new[] { " + string.Join(", ", GetOutputSensors(rule).Select(s => $"\"{s}\"")) + " },");
                sb.AppendLine($"                HasTemporalConditions = {HasTemporalConditions(rule).ToString().ToLower()}");
                sb.AppendLine("            },");
            }
            sb.AppendLine("        };");
            sb.AppendLine();

            // RuleInfo class
            sb.AppendLine("        public class RuleInfo");
            sb.AppendLine("        {");
            sb.AppendLine("            public string Name { get; set; }");
            sb.AppendLine("            public string Description { get; set; }");
            sb.AppendLine("            public int Layer { get; set; }");
            sb.AppendLine("            public string SourceFile { get; set; }");
            sb.AppendLine("            public int LineNumber { get; set; }");
            sb.AppendLine("            public string[] InputSensors { get; set; }");
            sb.AppendLine("            public string[] OutputSensors { get; set; }");
            sb.AppendLine("            public bool HasTemporalConditions { get; set; }");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return new Pulsar.Compiler.Models.GeneratedFileInfo
            {
                FileName = "RuleMetadata.cs",
                Content = sb.ToString(),
                Namespace = "Beacon.Runtime.Generated"
            };
        }

        public GeneratedFileInfo GenerateEmbeddedConfig(BuildConfig buildConfig)
        {
            string content = "// Embedded config for Pulsar Compiler" + Environment.NewLine;
            return new Pulsar.Compiler.Models.GeneratedFileInfo
            {
                FileName = System.IO.Path.Combine(buildConfig.OutputPath, "EmbeddedConfig.cs"),
                Content = content
            };
        }

        public List<GeneratedFileInfo> ApplyPostGenerationFixups(List<GeneratedFileInfo> files)
        {
            // Currently no post-generation fixups needed.
            return files;
        }

        private string GenerateFileHeader()
        {
            return "// Generated by Pulsar AOT Compiler";
        }

        private string GenerateCommonUsings()
        {
            var builder = new StringBuilder();
            builder.AppendLine("using System;");
            builder.AppendLine("using System.Collections.Generic;");
            builder.AppendLine("using System.Linq;");
            builder.AppendLine("using System.Threading.Tasks;");
            builder.AppendLine("using Microsoft.Extensions.Logging;");
            builder.AppendLine("using Prometheus;");
            builder.AppendLine("using StackExchange.Redis;");
            builder.AppendLine("using Pulsar.Runtime.Buffers;");
            builder.AppendLine("using Pulsar.Runtime.Rules;");
            return builder.ToString();
        }

        // Helper methods
        private List<string> GetInputSensors(RuleDefinition rule)
        {
            var sensors = new HashSet<string>();

            if (rule.Conditions != null)
            {
                if (rule.Conditions.All != null)
                {
                    foreach (var condition in rule.Conditions.All)
                    {
                        AddConditionSensors(condition, sensors);
                    }
                }

                if (rule.Conditions.Any != null)
                {
                    foreach (var condition in rule.Conditions.Any)
                    {
                        AddConditionSensors(condition, sensors);
                    }
                }
            }

            return sensors.ToList();
        }

        private List<string> GetOutputSensors(RuleDefinition rule)
        {
            return rule.Actions.OfType<SetValueAction>().Select(a => a.Key).ToList();
        }

        private bool HasTemporalConditions(RuleDefinition rule)
        {
            return rule.Conditions?.All?.Any(c => c is ThresholdOverTimeCondition) == true
                || rule.Conditions?.Any?.Any(c => c is ThresholdOverTimeCondition) == true;
        }

        private void AddConditionSensors(ConditionDefinition condition, HashSet<string> sensors)
        {
            switch (condition)
            {
                case ComparisonCondition c:
                    sensors.Add(c.Sensor);
                    break;
                case ThresholdOverTimeCondition t:
                    sensors.Add(t.Sensor);
                    break;
                case ExpressionCondition e:
                    sensors.UnionWith(ExtractSensorsFromExpression(e.Expression));
                    break;
            }
        }

        private HashSet<string> ExtractSensorsFromExpression(string expression)
        {
            var sensors = new HashSet<string>();
            if (string.IsNullOrWhiteSpace(expression))
                return sensors;

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

            return sensors;
        }

        private bool IsMathFunction(string functionName)
        {
            var mathFunctions = new HashSet<string> { "Sin", "Cos", "Tan", "Log", "Exp", "Sqrt", "Abs" };
            return mathFunctions.Contains(functionName);
        }

        public List<List<RuleDefinition>> SplitRulesIntoGroups(
            List<RuleDefinition> rules,
            Dictionary<string, string> layerMap)
        {
            if (rules == null || !rules.Any())
            {
                return new List<List<RuleDefinition>>();
            }

            var groups = new List<List<RuleDefinition>>();
            var currentGroup = new List<RuleDefinition>();
            var currentLayer = int.Parse(layerMap[rules[0].Name]);

            foreach (var rule in rules.OrderBy(r => int.Parse(layerMap[r.Name])))
            {
                var ruleLayer = int.Parse(layerMap[rule.Name]);
                if (ruleLayer != currentLayer && currentGroup.Any())
                {
                    groups.Add(currentGroup);
                    currentGroup = new List<RuleDefinition>();
                }

                currentGroup.Add(rule);
                currentLayer = ruleLayer;
            }

            if (currentGroup.Any())
            {
                groups.Add(currentGroup);
            }

            return groups;
        }

        // ----- Begin Beacon Project Generation Methods -----
        
        private GeneratedFileInfo GenerateBeaconSolution()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Microsoft Visual Studio Solution File, Format Version 12.00");
            sb.AppendLine("# Visual Studio Version 17\nVisualStudioVersion = 17.0.31903.59");
            sb.AppendLine("MinimumVisualStudioVersion = 10.0.40219.1");
            // Add project entries for Beacon.Runtime and Beacon.Tests
            sb.AppendLine("Project(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"Beacon.Runtime\", \"Beacon.Runtime.csproj\", \"{D1E3FBE2-1234-4F6E-9CDE-ABCDE1234567}\"");
            sb.AppendLine("EndProject");
            sb.AppendLine("Project(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"Beacon.Tests\", \"Beacon.Tests.csproj\", \"{E2F4CBE3-2345-5G7F-ADFE-BCDEF2345678}\"");
            sb.AppendLine("EndProject");
            sb.AppendLine("Global");
            sb.AppendLine("    GlobalSection(SolutionConfigurationPlatforms) = preSolution");
            sb.AppendLine("        Debug|Any CPU = Debug|Any CPU");
            sb.AppendLine("        Release|Any CPU = Release|Any CPU");
            sb.AppendLine("    EndGlobalSection");
            sb.AppendLine("    GlobalSection(ProjectConfigurationPlatforms) = postSolution");
            sb.AppendLine("        {D1E3FBE2-1234-4F6E-9CDE-ABCDE1234567}.Debug|Any CPU.ActiveCfg = Debug|Any CPU");
            sb.AppendLine("        {D1E3FBE2-1234-4F6E-9CDE-ABCDE1234567}.Debug|Any CPU.Build.0 = Debug|Any CPU");
            sb.AppendLine("        {E2F4CBE3-2345-5G7F-ADFE-BCDEF2345678}.Debug|Any CPU.ActiveCfg = Debug|Any CPU");
            sb.AppendLine("        {E2F4CBE3-2345-5G7F-ADFE-BCDEF2345678}.Debug|Any CPU.Build.0 = Debug|Any CPU");
            sb.AppendLine("        {D1E3FBE2-1234-4F6E-9CDE-ABCDE1234567}.Release|Any CPU.ActiveCfg = Release|Any CPU");
            sb.AppendLine("        {D1E3FBE2-1234-4F6E-9CDE-ABCDE1234567}.Release|Any CPU.Build.0 = Release|Any CPU");
            sb.AppendLine("        {E2F4CBE3-2345-5G7F-ADFE-BCDEF2345678}.Release|Any CPU.ActiveCfg = Release|Any CPU");
            sb.AppendLine("        {E2F4CBE3-2345-5G7F-ADFE-BCDEF2345678}.Release|Any CPU.Build.0 = Release|Any CPU");
            sb.AppendLine("    EndGlobalSection");
            sb.AppendLine("EndGlobal");
            
            return new Pulsar.Compiler.Models.GeneratedFileInfo
            {
                FileName = "Beacon.sln",
                Content = sb.ToString(),
                Namespace = "" // Not applicable for solution file
            };
        }

        public List<GeneratedFileInfo> GenerateAllFiles(List<RuleDefinition> rules, BuildConfig buildConfig)
        {
            if (buildConfig == null)
                throw new ArgumentNullException(nameof(buildConfig));

            var generatedFiles = new List<GeneratedFileInfo>();

            // Create solution and project files
            generatedFiles.Add(GenerateBeaconSolution());
            generatedFiles.Add(GenerateRuntimeProject(buildConfig));
            generatedFiles.Add(GenerateTestsProject(buildConfig));

            // Group rules and assign layers
            var layerMap = AssignLayers(rules);
            var stringLayerMap = layerMap.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString());
            var ruleGroups = SplitRulesIntoGroups(rules, stringLayerMap);

            // Generate rule group files
            for (int i = 0; i < ruleGroups.Count; i++)
            {
                var groupImplementation = GenerateGroupImplementation(i, ruleGroups[i], stringLayerMap);
                generatedFiles.Add(groupImplementation);
            }

            // Generate coordinator
            var coordinator = GenerateRuleCoordinator(ruleGroups, stringLayerMap);
            generatedFiles.Add(coordinator);

            // Generate metadata file
            var metadata = GenerateMetadataFile(rules, stringLayerMap);
            generatedFiles.Add(metadata);

            // Generate embedded config
            var config = GenerateEmbeddedConfig(buildConfig);
            generatedFiles.Add(config);

            // Generate test files
            var testFiles = GenerateTestFiles(rules, stringLayerMap);
            generatedFiles.AddRange(testFiles);

            // Apply any necessary post-generation fixups
            ApplyPostGenerationFixups(generatedFiles);

            return generatedFiles;
        }

        private GeneratedFileInfo GenerateRuntimeProject(BuildConfig buildConfig)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<Project Sdk=\"Microsoft.NET.Sdk\">");
            sb.AppendLine("  <PropertyGroup>");
            sb.AppendLine($"    <OutputType>{(buildConfig.StandaloneExecutable ? "Exe" : "Library")}</OutputType>");
            sb.AppendLine($"    <TargetFramework>{buildConfig.TargetFramework}</TargetFramework>");
            sb.AppendLine("    <ImplicitUsings>enable</ImplicitUsings>");
            sb.AppendLine("    <Nullable>enable</Nullable>");
            sb.AppendLine("    <PublishAot>true</PublishAot>");
            sb.AppendLine("  </PropertyGroup>");
            
            sb.AppendLine("  <ItemGroup>");
            sb.AppendLine("    <PackageReference Include=\"Microsoft.Extensions.Logging\" Version=\"8.0.0\" />");
            sb.AppendLine("    <PackageReference Include=\"StackExchange.Redis\" Version=\"2.7.10\" />");
            sb.AppendLine("    <PackageReference Include=\"Serilog\" Version=\"3.1.1\" />");
            sb.AppendLine("    <PackageReference Include=\"prometheus-net\" Version=\"8.2.1\" />");
            sb.AppendLine("  </ItemGroup>");
            
            sb.AppendLine("</Project>");

            return new Pulsar.Compiler.Models.GeneratedFileInfo
            {
                FileName = "Beacon.Runtime.csproj",
                Content = sb.ToString(),
                Namespace = "Beacon.Runtime"
            };
        }

        private GeneratedFileInfo GenerateTestsProject(BuildConfig buildConfig)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<Project Sdk=\"Microsoft.NET.Sdk\">");
            sb.AppendLine("  <PropertyGroup>");
            sb.AppendLine($"    <TargetFramework>{buildConfig.TargetFramework}</TargetFramework>");
            sb.AppendLine("    <ImplicitUsings>enable</ImplicitUsings>");
            sb.AppendLine("    <Nullable>enable</Nullable>");
            sb.AppendLine("    <IsTestProject>true</IsTestProject>");
            sb.AppendLine("  </PropertyGroup>");
            
            sb.AppendLine("  <ItemGroup>");
            sb.AppendLine("    <PackageReference Include=\"Microsoft.NET.Test.Sdk\" Version=\"17.9.0\" />");
            sb.AppendLine("    <PackageReference Include=\"xunit\" Version=\"2.7.0\" />");
            sb.AppendLine("    <PackageReference Include=\"xunit.runner.visualstudio\" Version=\"2.5.7\">");
            sb.AppendLine("      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>");
            sb.AppendLine("      <PrivateAssets>all</PrivateAssets>");
            sb.AppendLine("    </PackageReference>");
            sb.AppendLine("  </ItemGroup>");
            
            sb.AppendLine("  <ItemGroup>");
            sb.AppendLine("    <ProjectReference Include=\"../Beacon.Runtime/Beacon.Runtime.csproj\" />");
            sb.AppendLine("  </ItemGroup>");
            
            sb.AppendLine("</Project>");

            return new Pulsar.Compiler.Models.GeneratedFileInfo
            {
                FileName = "Beacon.Tests.csproj",
                Content = sb.ToString(),
                Namespace = "Beacon.Tests"
            };
        }

        public List<GeneratedFileInfo> GenerateTestFiles(List<RuleDefinition> rules, Dictionary<string, string> layerMap)
        {
            var testFiles = new List<GeneratedFileInfo>();
            var ruleGroups = SplitRulesIntoGroups(rules, layerMap);

            for (int i = 0; i < ruleGroups.Count; i++)
            {
                var groupTests = GenerateRuleGroupTests(i, ruleGroups[i], layerMap);
                testFiles.Add(groupTests);
            }

            return testFiles;
        }

        private GeneratedFileInfo GenerateRuleGroupTests(int layerId, List<RuleDefinition> rules, Dictionary<string, string> layerMap)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine("using Xunit;");
            sb.AppendLine("using Beacon.Runtime.Generated;");
            sb.AppendLine();

            sb.AppendLine("namespace Beacon.Tests.Generated");
            sb.AppendLine("{");
            sb.AppendLine($"    public class RuleGroup{layerId}Tests");
            sb.AppendLine("    {");

            foreach (var rule in rules)
            {
                sb.AppendLine($"        [Fact]");
                sb.AppendLine($"        public async Task {rule.Name}_ExecutesCorrectly()");
                sb.AppendLine("        {");
                sb.AppendLine("            // Arrange");
                sb.AppendLine("            var inputs = new Dictionary<string, object>();");
                sb.AppendLine("            var outputs = new Dictionary<string, object>();");

                // Add test inputs based on rule conditions
                if (rule.Conditions != null)
                {
                    foreach (var sensor in GetInputSensors(rule))
                    {
                        sb.AppendLine($"            inputs[\"{sensor}\"] = 42.0; // TODO: Generate appropriate test values");
                    }
                }

                sb.AppendLine();
                sb.AppendLine("            // Act");
                sb.AppendLine($"            var ruleGroup = new RuleGroup{layerId}(null, null, null);");
                sb.AppendLine("            await ruleGroup.EvaluateRulesAsync(inputs, outputs);");
                sb.AppendLine();
                sb.AppendLine("            // Assert");

                // Add assertions based on rule actions
                foreach (var sensor in GetOutputSensors(rule))
                {
                    sb.AppendLine($"            Assert.True(outputs.ContainsKey(\"{sensor}\"));");
                }

                sb.AppendLine("        }");
                sb.AppendLine();
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            return new Pulsar.Compiler.Models.GeneratedFileInfo
            {
                FileName = $"RuleGroup{layerId}Tests.cs",
                Content = sb.ToString(),
                Namespace = "Beacon.Tests.Generated"
            };
        }
        // ----- End Beacon Project Generation Methods -----
    }
}