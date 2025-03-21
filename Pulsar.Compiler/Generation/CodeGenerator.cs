// File: Pulsar.Compiler/Generation/CodeGenerator.cs
// NOTE: This implementation includes AOT compatibility fixes.

using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Pulsar.Compiler.Config;
using Pulsar.Compiler.Core;
using Pulsar.Compiler.Generation.Generators;
using Pulsar.Compiler.Models;

namespace Pulsar.Compiler.Generation
{
    public class CodeGenerator : IDisposable
    {
        private readonly ILogger<CodeGenerator> _logger;
        private readonly RuleGroupGenerator _ruleGroupGenerator;
        private readonly RuleCoordinatorGenerator _ruleCoordinatorGenerator;
        private readonly MetadataGenerator _metadataGenerator;

        public CodeGenerator(ILogger<CodeGenerator>? logger = null)
        {
            _logger = logger ?? NullLogger<CodeGenerator>.Instance;
            _ruleGroupGenerator = new RuleGroupGenerator(_logger);
            _ruleCoordinatorGenerator = new RuleCoordinatorGenerator(_logger);
            _metadataGenerator = new MetadataGenerator(_logger);
        }

        public void Dispose()
        {
            // No unmanaged resources to dispose
            GC.SuppressFinalize(this);
        }

        public List<Pulsar.Compiler.Models.GeneratedFileInfo> GenerateCSharp(
            List<RuleDefinition> rules,
            BuildConfig buildConfig
        )
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
                var groupImplementation = _ruleGroupGenerator.GenerateGroupImplementation(
                    i,
                    ruleGroups[i],
                    layerMap,
                    buildConfig
                );
                groupImplementation.Namespace = buildConfig.Namespace;
                generatedFiles.Add(groupImplementation);
            }

            // Generate rule coordinator
            var coordinator = _ruleCoordinatorGenerator.GenerateRuleCoordinator(
                ruleGroups,
                layerMap,
                buildConfig
            );
            coordinator.Namespace = buildConfig.Namespace;
            generatedFiles.Add(coordinator);

            // Generate metadata file
            var metadata = _metadataGenerator.GenerateMetadataFile(rules, layerMap, buildConfig);
            metadata.Namespace = buildConfig.Namespace;
            generatedFiles.Add(metadata);

            // Generate embedded config
            var embeddedConfig = GenerateEmbeddedConfig(buildConfig);
            embeddedConfig.Namespace = buildConfig.Namespace;
            generatedFiles.Add(embeddedConfig);

            // Generate Program.cs with AOT compatibility attributes
            var programFile = GenerateProgramFile(buildConfig);
            programFile.Namespace = buildConfig.Namespace;
            generatedFiles.Add(programFile);

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

        private List<string> GetDependencies(
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

        public GeneratedFileInfo GenerateEmbeddedConfig(BuildConfig buildConfig)
        {
            // Serialize system configuration to JSON
            string systemConfigJson = "{}";
            if (buildConfig.SystemConfig != null)
            {
                try
                {
                    // Escape double quotes to make it a valid C# string literal
                    systemConfigJson = System.Text.Json.JsonSerializer.Serialize(
                        buildConfig.SystemConfig
                    );
                    systemConfigJson = systemConfigJson.Replace("\"", "\\\"");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to serialize system configuration");
                }
            }

            string content = "// Embedded config for Pulsar Compiler" + Environment.NewLine;
            content += "using System;" + Environment.NewLine;
            content += Environment.NewLine;

            content += $"namespace {buildConfig.Namespace}.Generated" + Environment.NewLine;
            content += "{" + Environment.NewLine;
            content += "    public static class EmbeddedConfig" + Environment.NewLine;
            content += "    {" + Environment.NewLine;
            content +=
                $"        public const string ConfigJson = \"{systemConfigJson}\";"
                + Environment.NewLine;
            content += "    }" + Environment.NewLine;
            content += "}" + Environment.NewLine;

            return new Pulsar.Compiler.Models.GeneratedFileInfo
            {
                FileName = "EmbeddedConfig.cs",
                Content = content,
            };
        }

        public GeneratedFileInfo GenerateProgramFile(BuildConfig buildConfig)
        {
            var sb = new StringBuilder();

            // Add file header
            sb.AppendLine("// Auto-generated Program.cs");
            sb.AppendLine("// Generated: " + DateTime.UtcNow.ToString("O"));
            sb.AppendLine(
                "// This file contains the main entry point and AOT compatibility attributes"
            );
            sb.AppendLine();

            // Add standard using statements
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.IO;");
            sb.AppendLine("using System.Threading;");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine("using System.Runtime.CompilerServices;");
            sb.AppendLine("using System.Text.Json.Serialization;");
            sb.AppendLine("using System.Diagnostics.CodeAnalysis;");
            sb.AppendLine("using Microsoft.Extensions.Logging;");
            sb.AppendLine($"using {buildConfig.Namespace}.Buffers;");
            sb.AppendLine($"using {buildConfig.Namespace}.Services;");
            sb.AppendLine($"using {buildConfig.Namespace}.Interfaces;");
            sb.AppendLine($"using {buildConfig.Namespace}.Models;");
            sb.AppendLine();

            // Add namespace and class declaration
            sb.AppendLine($"namespace {buildConfig.Namespace}");
            sb.AppendLine("{");

            // Add serialization context class
            sb.AppendLine("    [JsonSerializable(typeof(Dictionary<string, object>))]");
            sb.AppendLine("    [JsonSerializable(typeof(Models.RuntimeConfig))]");
            sb.AppendLine("    [JsonSerializable(typeof(Models.RedisConfiguration))]");
            sb.AppendLine(
                "    public partial class SerializationContext : JsonSerializerContext { }"
            );
            sb.AppendLine();

            sb.AppendLine("    public class Program");
            sb.AppendLine("    {");

            // Add main method with AOT attributes
            sb.AppendLine(
                "        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RuntimeOrchestrator))]"
            );
            sb.AppendLine(
                "        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RedisService))]"
            );
            sb.AppendLine(
                "        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RuleCoordinator))]"
            );
            sb.AppendLine("        public static async Task Main(string[] args)");
            sb.AppendLine("        {");
            sb.AppendLine("            // Configure logging");
            sb.AppendLine("            var loggerFactory = LoggerFactory.Create(builder =>");
            sb.AppendLine("            {");
            sb.AppendLine("                builder.AddConsole();");
            sb.AppendLine("            });");
            sb.AppendLine("            var logger = loggerFactory.CreateLogger<Program>();");
            sb.AppendLine("            logger.LogInformation(\"Starting Beacon Runtime Engine\");");
            sb.AppendLine();
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine("                // Load configuration");
            sb.AppendLine("                var config = RuntimeConfig.LoadFromEnvironment();");
            sb.AppendLine(
                "                logger.LogInformation(\"Loaded configuration with {SensorCount} sensors\", config.ValidSensors.Count);"
            );
            sb.AppendLine();
            sb.AppendLine("                // Initialize Redis service");
            sb.AppendLine("                var redisConfig = config.Redis;");
            sb.AppendLine();
            sb.AppendLine("                // Create buffer manager for temporal rules");
            sb.AppendLine(
                "                var bufferManager = new RingBufferManager(config.BufferCapacity);"
            );
            sb.AppendLine();
            sb.AppendLine("                // Initialize runtime orchestrator");
            sb.AppendLine(
                "                using var redisService = new RedisService(redisConfig, loggerFactory);"
            );
            sb.AppendLine(
                "                var orchestratorLogger = loggerFactory.CreateLogger<RuntimeOrchestrator>();"
            );
            sb.AppendLine(
                "                var ruleCoordinatorLogger = loggerFactory.CreateLogger<RuleCoordinator>();"
            );
            sb.AppendLine("                var orchestrator = new RuntimeOrchestrator(");
            sb.AppendLine("                    redisService, ");
            sb.AppendLine("                    orchestratorLogger, ");
            sb.AppendLine(
                "                    new RuleCoordinator(redisService, ruleCoordinatorLogger, bufferManager)"
            );
            sb.AppendLine("                );");
            sb.AppendLine();
            sb.AppendLine("                // Start the orchestrator");
            sb.AppendLine("                await orchestrator.StartAsync();");
            sb.AppendLine("");
            sb.AppendLine("                // Wait for Ctrl+C");
            sb.AppendLine(
                "                var cancellationSource = new CancellationTokenSource();"
            );
            sb.AppendLine("                Console.CancelKeyPress += (sender, e) =>");
            sb.AppendLine("                {");
            sb.AppendLine(
                "                    logger.LogInformation(\"Application shutdown requested\");"
            );
            sb.AppendLine("                    cancellationSource.Cancel();");
            sb.AppendLine("                    e.Cancel = true;");
            sb.AppendLine("                };");
            sb.AppendLine("                ");
            sb.AppendLine("                // Wait until cancellation is requested");
            sb.AppendLine("                try");
            sb.AppendLine("                {");
            sb.AppendLine(
                "                    await Task.Delay(Timeout.Infinite, cancellationSource.Token);"
            );
            sb.AppendLine("                }");
            sb.AppendLine("                catch (OperationCanceledException)");
            sb.AppendLine("                {");
            sb.AppendLine("                    // Cancellation was requested");
            sb.AppendLine("                }");
            sb.AppendLine("                ");
            sb.AppendLine("                // Stop the orchestrator");
            sb.AppendLine("                await orchestrator.StopAsync();");
            sb.AppendLine("            }");
            sb.AppendLine("            catch (Exception ex)");
            sb.AppendLine("            {");
            sb.AppendLine(
                "                logger.LogError(ex, \"Fatal error in Beacon Runtime Engine\");"
            );
            sb.AppendLine("                Environment.ExitCode = 1;");
            sb.AppendLine("            }");
            sb.AppendLine("            finally");
            sb.AppendLine("            {");
            sb.AppendLine(
                "                logger.LogInformation(\"Beacon Runtime Engine stopped\");"
            );
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return new Pulsar.Compiler.Models.GeneratedFileInfo
            {
                FileName = "Program.cs",
                Content = sb.ToString(),
                Namespace = buildConfig.Namespace,
            };
        }

        // Removed unused methods to match CodeGeneratorFixed implementation

        public List<List<RuleDefinition>> SplitRulesIntoGroups(
            List<RuleDefinition> rules,
            Dictionary<string, string> layerMap
        )
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
    }
}
