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
            BuildConfig? buildConfig = null)
        {
            buildConfig ??= new BuildConfig
            {
                OutputPath = "Generated",
                RulesPath = "Rules",
                Target = "win-x64",
                ProjectName = "Pulsar.Compiler",
                TargetFramework = "net9.0",
            };

            var analyzer = new DependencyAnalyzer();
            var layerMap = analyzer.GetDependencyMap(rules);
            var ruleGroups = SplitRulesIntoGroups(rules, layerMap, new RuleGroupingConfig());

            var generatedFiles = new List<Pulsar.Compiler.Models.GeneratedFileInfo>();

            // Generate rule groups
            foreach (var group in ruleGroups)
            {
                var groupImplementation = GenerateGroupImplementation(group.Key, group.Value, layerMap);
                generatedFiles.Add(groupImplementation);
            }

            // Generate rule coordinator
            var coordinator = GenerateRuleCoordinator(ruleGroups, layerMap);
            generatedFiles.Add(coordinator);

            // Generate metadata file
            var metadata = GenerateMetadataFile(rules, layerMap);
            generatedFiles.Add(metadata);

            // Generate embedded config
            var embeddedConfig = GenerateEmbeddedConfig(buildConfig);
            generatedFiles.Add(embeddedConfig);

            return generatedFiles;
        }

        public static Dictionary<int, List<RuleDefinition>> SplitRulesIntoGroups(
            List<RuleDefinition> rules,
            Dictionary<string, string> layerMap,
            RuleGroupingConfig config)
        {
            var groups = new Dictionary<int, List<RuleDefinition>>();
            var currentGroupIndex = 0;

            // Sort rules by layer to maintain dependency order
            var sortedRules = rules.OrderBy(r => int.Parse(layerMap[r.Name])).ToList();

            if (config.GroupParallelRules)
            {
                var rulesByLayer = sortedRules
                    .GroupBy(r => int.Parse(layerMap[r.Name]))
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
                var currentLayer = int.Parse(layerMap[sortedRules.First().Name]);

                foreach (var rule in sortedRules)
                {
                    var ruleLayer = int.Parse(layerMap[rule.Name]);

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

        private static Dictionary<string, int> GetRulesByLayer(
            List<RuleDefinition> rules,
            Dictionary<string, string> layerMap
        )
        {
            return rules.GroupBy(r => int.Parse(layerMap[r.Name])).ToDictionary(g => g.Key, g => g.ToList());
        }

        public static Pulsar.Compiler.Models.GeneratedFileInfo GenerateGroupImplementation(
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
            sb.AppendLine("using Pulsar.Runtime.Buffers;");
            sb.AppendLine("using Pulsar.Runtime.Services;");
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
            var requiredSensors = rules.SelectMany(r => GetRequiredSensors(r)).Distinct().ToList();
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

        private static string GenerateCondition(ConditionGroup? conditions)
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

        private static string GenerateConditionExpression(ConditionDefinition condition)
        {
            return condition switch
            {
                ComparisonCondition comparison => GenerateComparisonCondition(comparison),
                ExpressionCondition expression => FixupExpression(expression.Expression),
                ThresholdOverTimeCondition threshold => GenerateThresholdCondition(threshold),
                _ => throw new InvalidOperationException($"Unknown condition type: {condition.GetType().Name}")
            };
        }

        private static string GenerateComparisonCondition(ComparisonCondition comparison)
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

        private static string GenerateThresholdCondition(ThresholdOverTimeCondition threshold)
        {
            return $"CheckThreshold(\"{threshold.Sensor}\", {threshold.Threshold}, {threshold.Duration}, \"{threshold.ComparisonOperator}\")";
        }

        private static string GenerateAction(ActionDefinition action)
        {
            return action switch
            {
                SetValueAction setValue => GenerateSetValueAction(setValue),
                _ => throw new InvalidOperationException($"Unknown action type: {action.GetType().Name}")
            };
        }

        private static string GenerateSetValueAction(SetValueAction setValue)
        {
            var value = !string.IsNullOrEmpty(setValue.ValueExpression)
                ? FixupExpression(setValue.ValueExpression)
                : setValue.Value.ToString();

            return $"outputs[\"{setValue.Key}\"] = {value};";
        }

        private static string FixupExpression(string expression)
        {
            // Replace sensor references with dictionary lookups
            return Regex.Replace(
                expression,
                @"\$([a-zA-Z_][a-zA-Z0-9_]*)",
                m => $"Convert.ToDouble(inputs[\"{m.Groups[1].Value}\"])"
            );
        }

        private static IEnumerable<string> GetRequiredSensors(RuleDefinition rule)
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

            return sensors;
        }

        private static void AddConditionSensors(ConditionDefinition condition, HashSet<string> sensors)
        {
            switch (condition)
            {
                case ComparisonCondition comparison:
                    sensors.Add(comparison.Sensor);
                    break;

                case ExpressionCondition expression:
                    // Extract sensor names from expression using regex
                    var matches = Regex.Matches(expression.Expression, @"inputs\[""([^""]+)""\]");
                    foreach (Match match in matches)
                    {
                        sensors.Add(match.Groups[1].Value);
                    }
                    break;

                case ThresholdOverTimeCondition threshold:
                    sensors.Add(threshold.Sensor);
                    break;
            }
        }

        public static Pulsar.Compiler.Models.GeneratedFileInfo GenerateRuleCoordinator(
            Dictionary<int, List<RuleDefinition>> ruleGroups,
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
            sb.AppendLine("using Pulsar.Runtime.Buffers;");
            sb.AppendLine("using Pulsar.Runtime.Services;");
            sb.AppendLine("using Pulsar.Runtime.Rules;");
            sb.AppendLine("using Pulsar.Runtime.Interfaces;");
            sb.AppendLine();

            sb.AppendLine("namespace Beacon.Runtime.Generated");
            sb.AppendLine("{");

            // Class declaration
            sb.AppendLine("    public class GeneratedRuleCoordinator : TemplateRuleCoordinator");
            sb.AppendLine("    {");

            // Constructor
            sb.AppendLine("        public GeneratedRuleCoordinator(");
            sb.AppendLine("            IRedisService redis,");
            sb.AppendLine("            ILogger logger,");
            sb.AppendLine("            RingBufferManager bufferManager)");
            sb.AppendLine("            : base(redis, logger, bufferManager)");
            sb.AppendLine("        {");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Initialize rule groups
            sb.AppendLine("        protected override void InitializeRuleGroups()");
            sb.AppendLine("        {");
            foreach (var group in ruleGroups)
            {
                sb.AppendLine($"            AddRuleGroup(new RuleGroup{group.Key}(_redis, _logger, _bufferManager));");
            }
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return new Pulsar.Compiler.Models.GeneratedFileInfo
            {
                FileName = "GeneratedRuleCoordinator.cs",
                Content = sb.ToString(),
                Namespace = "Beacon.Runtime.Generated"
            };
        }

        public static Pulsar.Compiler.Models.GeneratedFileInfo GenerateMetadataFile(
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
                    SourceFile = rule.SourceFile,
                    SourceLineNumber = rule.LineNumber,
                    Layer = int.Parse(layerMap[rule.Name]),
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

        public static Pulsar.Compiler.Models.GeneratedFileInfo GenerateEmbeddedConfig(BuildConfig buildConfig)
        {
            string content = "// Embedded config for Pulsar Compiler" + Environment.NewLine;
            return new Pulsar.Compiler.Models.GeneratedFileInfo
            {
                FileName = System.IO.Path.Combine(buildConfig.OutputPath, "EmbeddedConfig.cs"),
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

        private static string GenerateCommonUsings()
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

        private static bool IsMathFunction(string functionName)
        {
            var mathFunctions = new HashSet<string> { "Sin", "Cos", "Tan", "Log", "Exp", "Sqrt", "Abs" };
            return mathFunctions.Contains(functionName);
        }

        public class SerilogAdapter : Microsoft.Extensions.Logging.ILogger
        {
            private readonly Serilog.ILogger _logger;
            
            public SerilogAdapter(Serilog.ILogger logger)
            {
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            }

            public IDisposable BeginScope<TState>(TState state)
            {
                return new LogScope();
            }

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                var level = logLevel switch
                {
                    LogLevel.Trace => Serilog.Events.LogEventLevel.Verbose,
                    LogLevel.Debug => Serilog.Events.LogEventLevel.Debug,
                    LogLevel.Information => Serilog.Events.LogEventLevel.Information,
                    LogLevel.Warning => Serilog.Events.LogEventLevel.Warning,
                    LogLevel.Error => Serilog.Events.LogEventLevel.Error,
                    LogLevel.Critical => Serilog.Events.LogEventLevel.Fatal,
                    _ => Serilog.Events.LogEventLevel.Information
                };

                _logger.Write(level, exception, formatter(state, exception));
            }

            private class LogScope : IDisposable
            {
                public void Dispose() { }
            }
        }

        // ----- Begin Beacon Project Generation Methods -----
        
        private static Pulsar.Compiler.Models.GeneratedFileInfo GenerateBeaconSolution()
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

        private static Pulsar.Compiler.Models.GeneratedFileInfo GenerateRuntimeProject()
        {
            var sb = new StringBuilder();
            sb.AppendLine("<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>");
            sb.AppendLine("    <OutputType>Exe</OutputType>");
            sb.AppendLine("    <TargetFramework>net6.0</TargetFramework>");
            sb.AppendLine("  </PropertyGroup>\n</Project>");
            
            return new Pulsar.Compiler.Models.GeneratedFileInfo
            {
                FileName = "Beacon.Runtime.csproj",
                Content = sb.ToString(),
                Namespace = "" // Not applicable for project files
            };
        }

        private static Pulsar.Compiler.Models.GeneratedFileInfo GenerateTestsProject()
        {
            var sb = new StringBuilder();
            sb.AppendLine("<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>");
            sb.AppendLine("    <TargetFramework>net6.0</TargetFramework>");
            sb.AppendLine("    <IsTestProject>true</IsTestProject>");
            sb.AppendLine("  </PropertyGroup>\n</Project>");
            
            return new Pulsar.Compiler.Models.GeneratedFileInfo
            {
                FileName = "Beacon.Tests.csproj",
                Content = sb.ToString(),
                Namespace = "" // Not applicable for project files
            };
        }

        // Modify the main generation logic to include the Beacon solution and project files in the output
        public List<Pulsar.Compiler.Models.GeneratedFileInfo> GenerateAllFiles(List<RuleDefinition> rules, BuildConfig buildConfig)
        {
            var files = GenerateCSharp(rules, buildConfig);

            // Add the Beacon solution and project files
            files.Add(GenerateBeaconSolution());
            files.Add(GenerateRuntimeProject());
            files.Add(GenerateTestsProject());

            return files;
        }

        // ----- End Beacon Project Generation Methods -----
    }
}