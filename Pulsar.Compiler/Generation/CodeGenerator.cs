// File: Pulsar.Compiler/Generation/CodeGenerator.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Pulsar.Compiler.Config;
using Pulsar.Compiler.Core;
using Pulsar.Compiler.Generation.Generators;
using Pulsar.Compiler.Generation.Helpers;
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
            string content = "// Embedded config for Pulsar Compiler" + Environment.NewLine;
            return new Pulsar.Compiler.Models.GeneratedFileInfo
            {
                FileName = System.IO.Path.Combine(buildConfig.OutputPath, "EmbeddedConfig.cs"),
                Content = content,
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
            builder.AppendLine("using Beacon.Runtime.Buffers;");
            builder.AppendLine("using Beacon.Runtime.Services;");
            builder.AppendLine("using Beacon.Runtime.Rules;");
            builder.AppendLine("using Beacon.Runtime.Interfaces;");
            return builder.ToString();
        }

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
