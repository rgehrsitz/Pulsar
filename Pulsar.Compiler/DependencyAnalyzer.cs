using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Pulsar.Compiler.Models;

namespace Pulsar.Compiler.Core
{
    public class DependencyAnalyzer
    {
        private readonly ILogger<DependencyAnalyzer> _logger;
        private Dictionary<string, RuleDefinition> _outputs = new();
        private readonly int _maxDependencyDepth;
        private readonly Dictionary<string, HashSet<string>> _temporalDependencies = new();

        public DependencyAnalyzer(int maxDependencyDepth = 10, ILogger<DependencyAnalyzer>? logger = null)
        {
            _logger = logger ?? NullLogger<DependencyAnalyzer>.Instance;
            _maxDependencyDepth = maxDependencyDepth;
        }

        public class DependencyValidationResult
        {
            public bool IsValid { get; set; }
            public List<List<string>> CircularDependencies { get; set; } = new();
            public List<List<string>> DeepDependencyChains { get; set; } = new();
            public Dictionary<string, int> RuleComplexityScores { get; set; } = new();
            public Dictionary<string, HashSet<string>> TemporalDependencies { get; set; } = new();
        }

        public DependencyValidationResult ValidateDependencies(List<RuleDefinition> rules)
        {
            var result = new DependencyValidationResult { IsValid = true };
            var graph = BuildGraph(rules);

            // Check for circular dependencies
            var cycles = FindCircularDependencies(graph);
            if (cycles.Any())
            {
                result.IsValid = false;
                result.CircularDependencies = cycles;
                foreach (var cycle in cycles)
                {
                    _logger.LogError("Circular dependency detected: {Path}", string.Join(" -> ", cycle));
                }
            }

            // Check dependency depths
            var deepChains = FindDeepDependencyChains(graph);
            if (deepChains.Any())
            {
                result.DeepDependencyChains = deepChains;
                foreach (var chain in deepChains)
                {
                    _logger.LogWarning("Deep dependency chain detected: {Path}", string.Join(" -> ", chain));
                }
            }

            // Calculate complexity scores
            result.RuleComplexityScores = CalculateRuleComplexity(rules, graph);

            // Track temporal dependencies
            result.TemporalDependencies = _temporalDependencies;

            return result;
        }

        public List<RuleDefinition> AnalyzeDependencies(List<RuleDefinition> rules)
        {
            try
            {
                var graph = BuildGraph(rules);
                var sortedRules = TopologicalSort(graph, rules);
                return sortedRules;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing rule dependencies");
                throw;
            }
        }

        private Dictionary<string, HashSet<string>> BuildGraph(List<RuleDefinition> rules)
        {
            var graph = new Dictionary<string, HashSet<string>>();

            foreach (var rule in rules)
            {
                if (!graph.ContainsKey(rule.Name))
                {
                    graph[rule.Name] = new HashSet<string>();
                }

                var dependencies = GetDependencies(rule, rules.ToDictionary(r => r.Name, r => r));
                foreach (var dependency in dependencies)
                {
                    if (!rules.Any(r => r.Name == dependency))
                    {
                        _logger.LogWarning("Rule {RuleName} depends on non-existent rule {DependencyName}", rule.Name, dependency);
                        continue;
                    }

                    graph[rule.Name].Add(dependency);
                }
            }

            return graph;
        }

        private List<List<string>> FindCircularDependencies(Dictionary<string, HashSet<string>> graph)
        {
            var cycles = new List<List<string>>();
            var visited = new HashSet<string>();
            var path = new List<string>();

            foreach (var rule in graph.Keys)
            {
                if (!visited.Contains(rule))
                {
                    DetectCycle(rule, graph, visited, path, cycles);
                }
            }

            return cycles;
        }

        private void DetectCycle(
            string current,
            Dictionary<string, HashSet<string>> graph,
            HashSet<string> visited,
            List<string> path,
            List<List<string>> cycles)
        {
            if (path.Contains(current))
            {
                var cycleStart = path.IndexOf(current);
                var cycle = path.Skip(cycleStart)
                               .Concat(new[] { current })
                               .ToList();
                cycles.Add(cycle);
                return;
            }

            if (visited.Contains(current))
            {
                return;
            }

            visited.Add(current);
            path.Add(current);

            foreach (var dependency in graph[current])
            {
                DetectCycle(dependency, graph, visited, path, cycles);
            }

            path.RemoveAt(path.Count - 1);
        }

        private List<List<string>> FindDeepDependencyChains(Dictionary<string, HashSet<string>> graph)
        {
            var deepChains = new List<List<string>>();
            var visited = new HashSet<string>();
            var path = new List<string>();

            foreach (var rule in graph.Keys)
            {
                if (!visited.Contains(rule))
                {
                    FindLongPaths(rule, graph, visited, path, deepChains);
                }
            }

            return deepChains;
        }

        private void FindLongPaths(
            string current,
            Dictionary<string, HashSet<string>> graph,
            HashSet<string> visited,
            List<string> path,
            List<List<string>> deepChains)
        {
            path.Add(current);

            if (path.Count > _maxDependencyDepth)
            {
                deepChains.Add(path.ToList());
            }

            if (!visited.Contains(current))
            {
                visited.Add(current);

                foreach (var dependency in graph[current])
                {
                    FindLongPaths(dependency, graph, visited, path, deepChains);
                }

                visited.Remove(current);
            }

            path.RemoveAt(path.Count - 1);
        }

        private Dictionary<string, int> CalculateRuleComplexity(
            List<RuleDefinition> rules,
            Dictionary<string, HashSet<string>> graph)
        {
            var scores = new Dictionary<string, int>();

            foreach (var rule in rules)
            {
                var score = 0;

                // Base complexity
                score += rule.Conditions?.All?.Count ?? 0;
                score += rule.Conditions?.Any?.Count ?? 0;
                score += rule.Actions.Count;

                // Dependency complexity
                score += CalculateDependencyDepth(rule, graph);

                scores[rule.Name] = score;
            }

            return scores;
        }

        private int CalculateDependencyDepth(
            RuleDefinition rule,
            Dictionary<string, HashSet<string>> graph)
        {
            var visited = new HashSet<string>();
            var depth = 0;
            var queue = new Queue<(string Rule, int Depth)>();
            queue.Enqueue((rule.Name, 0));

            while (queue.Count > 0)
            {
                var (current, currentDepth) = queue.Dequeue();

                if (!visited.Contains(current))
                {
                    visited.Add(current);
                    depth = Math.Max(depth, currentDepth);

                    foreach (var dependency in graph[current])
                    {
                        queue.Enqueue((dependency, currentDepth + 1));
                    }
                }
            }

            return depth;
        }

        public Dictionary<string, string> GetDependencyMap(List<RuleDefinition> rules)
        {
            var layerMap = BuildDependencyGraph(rules);
            
            // Convert int values to strings for AOT compatibility
            return layerMap.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ToString()
            );
        }

        private Dictionary<string, int> BuildDependencyGraph(List<RuleDefinition> rules)
        {
            var graph = BuildGraph(rules);
            var layerMap = new Dictionary<string, int>();
            var visited = new HashSet<string>();
            var visiting = new HashSet<string>();

            foreach (var rule in rules)
            {
                if (!layerMap.ContainsKey(rule.Name))
                {
                    AssignLayerDFS(rule.Name, graph, layerMap, visited, visiting);
                }
            }

            return layerMap;
        }

        private void AssignLayerDFS(
            string ruleName,
            Dictionary<string, HashSet<string>> graph,
            Dictionary<string, int> layerMap,
            HashSet<string> visited,
            HashSet<string> visiting)
        {
            if (visiting.Contains(ruleName))
            {
                _logger.LogError("Cyclic dependency detected involving rule {RuleName}", ruleName);
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

        private List<RuleDefinition> TopologicalSort(Dictionary<string, HashSet<string>> graph, List<RuleDefinition> rules)
        {
            var visited = new HashSet<string>();
            var visiting = new HashSet<string>();
            var sorted = new List<string>();

            foreach (var rule in rules)
            {
                if (!visited.Contains(rule.Name))
                {
                    TopologicalSortVisit(rule.Name, graph, visited, visiting, sorted);
                }
            }

            sorted.Reverse();
            return sorted.Select(name => rules.First(r => r.Name == name)).ToList();
        }

        private void TopologicalSortVisit(
            string ruleName,
            Dictionary<string, HashSet<string>> graph,
            HashSet<string> visited,
            HashSet<string> visiting,
            List<string> sorted)
        {
            if (visiting.Contains(ruleName))
            {
                _logger.LogError("Cyclic dependency detected involving rule {RuleName}", ruleName);
                throw new InvalidOperationException($"Cyclic dependency detected involving rule '{ruleName}'");
            }

            if (visited.Contains(ruleName))
            {
                return;
            }

            visiting.Add(ruleName);

            foreach (var dependency in graph[ruleName])
            {
                TopologicalSortVisit(dependency, graph, visited, visiting, sorted);
            }

            visiting.Remove(ruleName);
            visited.Add(ruleName);
            sorted.Add(ruleName);
        }

        private HashSet<string> GetDependencies(RuleDefinition rule, Dictionary<string, RuleDefinition> rules)
        {
            var dependencies = new HashSet<string>();

            // Check condition dependencies
            if (rule.Conditions != null)
            {
                if (rule.Conditions.All != null)
                {
                    foreach (var condition in rule.Conditions.All)
                    {
                        dependencies.UnionWith(GetConditionDependencies(condition, rules));
                    }
                }

                if (rule.Conditions.Any != null)
                {
                    foreach (var condition in rule.Conditions.Any)
                    {
                        dependencies.UnionWith(GetConditionDependencies(condition, rules));
                    }
                }
            }

            // Check action dependencies
            if (rule.Actions != null)
            {
                foreach (var action in rule.Actions)
                {
                    dependencies.UnionWith(GetActionDependencies(action, rules));
                }
            }

            return dependencies;
        }

        private HashSet<string> GetConditionDependencies(ConditionDefinition condition, Dictionary<string, RuleDefinition> rules)
        {
            var dependencies = new HashSet<string>();

            switch (condition)
            {
                case ComparisonCondition comparison:
                    dependencies.Add(comparison.Sensor);
                    break;

                case ExpressionCondition expression:
                    dependencies.UnionWith(ExtractSensorsFromExpression(expression.Expression));
                    break;

                case ThresholdOverTimeCondition threshold:
                    dependencies.Add(threshold.Sensor);
                    _temporalDependencies[threshold.Sensor] = new HashSet<string>();
                    break;
            }

            return dependencies;
        }

        private HashSet<string> GetActionDependencies(ActionDefinition action, Dictionary<string, RuleDefinition> rules)
        {
            var dependencies = new HashSet<string>();

            switch (action)
            {
                case SetValueAction set:
                    if (!string.IsNullOrEmpty(set.ValueExpression))
                    {
                        dependencies.UnionWith(ExtractSensorsFromExpression(set.ValueExpression));
                    }
                    break;
            }

            return dependencies;
        }

        private HashSet<string> ExtractSensorsFromExpression(string expression)
        {
            var sensors = new HashSet<string>();
            var sensorPattern = @"sensor\[([^\]]+)\]";
            var matches = Regex.Matches(expression, sensorPattern);

            foreach (Match match in matches)
            {
                if (match.Groups.Count > 1)
                {
                    sensors.Add(match.Groups[1].Value.Trim());
                }
            }

            return sensors;
        }
    }
}
