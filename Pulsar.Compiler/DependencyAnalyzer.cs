// File: Pulsar.Compiler/DependencyAnalyzer.cs

using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Pulsar.Compiler.Models;

namespace Pulsar.Compiler.Core
{
    public class DependencyAnalyzer(
        int maxDependencyDepth = 10,
        ILogger<DependencyAnalyzer>? logger = null)
        : IDisposable
    {
        private readonly ILogger<DependencyAnalyzer> _logger = logger ?? NullLogger<DependencyAnalyzer>.Instance;
        private bool _disposed;

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }

        private readonly Dictionary<string, HashSet<string>> _temporalDependencies = new();
        private static readonly HashSet<string> _mathFunctions = new()
        {
            "Sin",
            "Cos",
            "Tan",
            "Log",
            "Exp",
            "Sqrt",
            "Abs",
            "Max",
            "Min",
        };

        public DependencyValidationResult ValidateDependencies(List<RuleDefinition> rules)
        {
            var result = new DependencyValidationResult { IsValid = true };

            // Build a graph of rule dependencies
            var graph = BuildGraph(rules);

            // Create a map of sensor outputs to rules
            var sensorToRuleMap = new Dictionary<string, string>();
            foreach (var rule in rules)
            {
                foreach (var action in rule.Actions.OfType<SetValueAction>())
                {
                    sensorToRuleMap[action.Key] = rule.Name;
                }
            }

            // Check for circular dependencies
            var cycles = FindCircularDependencies(graph);
            if (cycles.Any())
            {
                result.IsValid = false;
                result.CircularDependencies = cycles;
                foreach (var cycle in cycles)
                {
                    _logger.LogError(
                        "Circular dependency detected: {Path}",
                        string.Join(" -> ", cycle)
                    );
                }
            }

            // Check dependency depths
            var deepChains = FindDeepDependencyChains(graph);
            if (deepChains.Any())
            {
                result.DeepDependencyChains = deepChains;
                foreach (var chain in deepChains)
                {
                    _logger.LogWarning(
                        "Deep dependency chain detected: {Path}",
                        string.Join(" -> ", chain)
                    );
                }
            }

            // Calculate complexity scores
            result.RuleComplexityScores = CalculateRuleComplexity(rules, graph);

            // Process temporal dependencies
            var temporalDependencies = new Dictionary<string, HashSet<string>>();

            foreach (var rule in rules)
            {
                // Process "All" conditions for temporal dependencies
                if (rule.Conditions?.All != null)
                {
                    foreach (
                        var condition in rule.Conditions.All.OfType<ThresholdOverTimeCondition>()
                    )
                    {
                        if (!temporalDependencies.ContainsKey(rule.Name))
                        {
                            temporalDependencies[rule.Name] = new HashSet<string>();
                        }
                        temporalDependencies[rule.Name].Add(condition.Sensor);

                        _logger.LogDebug(
                            "Added temporal dependency: Rule {Rule} depends on sensor {Sensor}",
                            rule.Name,
                            condition.Sensor
                        );
                    }
                }

                // Process "Any" conditions for temporal dependencies
                if (rule.Conditions?.Any != null)
                {
                    foreach (
                        var condition in rule.Conditions.Any.OfType<ThresholdOverTimeCondition>()
                    )
                    {
                        if (!temporalDependencies.ContainsKey(rule.Name))
                        {
                            temporalDependencies[rule.Name] = new HashSet<string>();
                        }
                        temporalDependencies[rule.Name].Add(condition.Sensor);

                        _logger.LogDebug(
                            "Added temporal dependency: Rule {Rule} depends on sensor {Sensor}",
                            rule.Name,
                            condition.Sensor
                        );
                    }
                }
            }

            result.TemporalDependencies = temporalDependencies;

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
            var sensorToRuleMap = new Dictionary<string, string>();

            // First, build a map of sensors to the rules that produce them
            foreach (var rule in rules)
            {
                foreach (var action in rule.Actions.OfType<SetValueAction>())
                {
                    sensorToRuleMap[action.Key] = rule.Name;
                }
            }

            // Then, build the dependency graph
            foreach (var rule in rules)
            {
                if (!graph.ContainsKey(rule.Name))
                {
                    graph[rule.Name] = new HashSet<string>();
                }

                // Check condition dependencies
                if (rule.Conditions != null)
                {
                    // Process "All" conditions
                    if (rule.Conditions.All != null)
                    {
                        foreach (var condition in rule.Conditions.All)
                        {
                            AddConditionDependencies(condition, rule.Name, sensorToRuleMap, graph);
                        }
                    }

                    // Process "Any" conditions
                    if (rule.Conditions.Any != null)
                    {
                        foreach (var condition in rule.Conditions.Any)
                        {
                            AddConditionDependencies(condition, rule.Name, sensorToRuleMap, graph);
                        }
                    }
                }
            }

            return graph;
        }

        private void AddConditionDependencies(
            ConditionDefinition condition,
            string ruleName,
            Dictionary<string, string> sensorToRuleMap,
            Dictionary<string, HashSet<string>> graph
        )
        {
            switch (condition)
            {
                case ComparisonCondition comparison:
                    if (sensorToRuleMap.TryGetValue(comparison.Sensor, out var producerRule))
                    {
                        graph[ruleName].Add(producerRule);
                        _logger.LogDebug(
                            "Rule {RuleName} depends on rule {DependencyName} via sensor {Sensor}",
                            ruleName,
                            producerRule,
                            comparison.Sensor
                        );
                    }
                    break;

                case ExpressionCondition expression:
                    var sensors = ExtractSensorsFromExpression(expression.Expression);
                    foreach (var sensor in sensors)
                    {
                        if (sensorToRuleMap.TryGetValue(sensor, out var producer))
                        {
                            graph[ruleName].Add(producer);
                            _logger.LogDebug(
                                "Rule {RuleName} depends on rule {DependencyName} via sensor {Sensor} in expression",
                                ruleName,
                                producer,
                                sensor
                            );
                        }
                    }
                    break;

                case ThresholdOverTimeCondition threshold:
                    if (sensorToRuleMap.TryGetValue(threshold.Sensor, out var thresholdProducer))
                    {
                        graph[ruleName].Add(thresholdProducer);
                        _logger.LogDebug(
                            "Rule {RuleName} depends on rule {DependencyName} via temporal sensor {Sensor}",
                            ruleName,
                            thresholdProducer,
                            threshold.Sensor
                        );
                    }
                    break;
            }
        }

        private List<List<string>> FindCircularDependencies(
            Dictionary<string, HashSet<string>> graph
        )
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
            List<List<string>> cycles
        )
        {
            // Added debug output for cycle detection
            _logger.LogDebug(
                "DetectCycle called: current={Current}, path={Path}",
                current,
                string.Join("->", path)
            );
            Console.WriteLine(
                $"[DEBUG] DetectCycle: current={current} path={string.Join("->", path)}"
            );

            // Use a separate set for tracking the current path to detect cycles
            var currentPath = new HashSet<string>(path);

            if (currentPath.Contains(current))
            {
                var cycleStart = path.IndexOf(current);
                var cycle = path.Skip(cycleStart).Concat(new[] { current }).ToList();
                _logger.LogError("Cycle found: {Cycle}", string.Join(" -> ", cycle));
                Console.WriteLine($"[DEBUG] Cycle found: {string.Join(" -> ", cycle)}");
                cycles.Add(cycle);
                return;
            }

            if (visited.Contains(current))
            {
                return;
            }

            visited.Add(current);
            path.Add(current);

            if (graph.ContainsKey(current))
            {
                foreach (var dependency in graph[current])
                {
                    DetectCycle(dependency, graph, visited, path, cycles);
                }
            }

            path.RemoveAt(path.Count - 1);
        }

        private List<List<string>> FindDeepDependencyChains(
            Dictionary<string, HashSet<string>> graph
        )
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
            List<List<string>> deepChains
        )
        {
            path.Add(current);

            // Check if the current path exceeds the maximum depth
            if (path.Count > maxDependencyDepth)
            {
                _logger.LogWarning(
                    "Deep dependency chain detected: {Path}",
                    string.Join(" -> ", path)
                );
                deepChains.Add(new List<string>(path)); // Create a copy of the path
            }

            // Only continue if the current node is not already visited
            if (!visited.Contains(current))
            {
                // Mark as visited for this path
                visited.Add(current);

                // Process dependencies if they exist
                if (graph.ContainsKey(current))
                {
                    foreach (var dependency in graph[current])
                    {
                        FindLongPaths(dependency, graph, visited, path, deepChains);
                    }
                }

                // Backtrack: remove from visited to allow this node in other paths
                visited.Remove(current);
            }

            // Backtrack: remove from current path
            path.RemoveAt(path.Count - 1);
        }

        private Dictionary<string, int> CalculateRuleComplexity(
            List<RuleDefinition> rules,
            Dictionary<string, HashSet<string>> graph
        )
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
            Dictionary<string, HashSet<string>> graph
        )
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
            return layerMap.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString());
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
            HashSet<string> visiting
        )
        {
            if (visiting.Contains(ruleName))
            {
                _logger.LogError("Cyclic dependency detected involving rule {RuleName}", ruleName);
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

        private List<RuleDefinition> TopologicalSort(
            Dictionary<string, HashSet<string>> graph,
            List<RuleDefinition> rules
        )
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
            List<string> sorted
        )
        {
            if (visiting.Contains(ruleName))
            {
                _logger.LogError("Cyclic dependency detected involving rule {RuleName}", ruleName);
                throw new InvalidOperationException(
                    $"Cyclic dependency detected involving rule '{ruleName}'"
                );
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

        private HashSet<string> GetDependencies(
            RuleDefinition rule,
            Dictionary<string, RuleDefinition> rules
        )
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

        private HashSet<string> GetConditionDependencies(
            ConditionDefinition condition,
            Dictionary<string, RuleDefinition> rules
        )
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

                    // Track temporal dependencies properly
                    if (!_temporalDependencies.ContainsKey(threshold.Sensor))
                    {
                        _temporalDependencies[threshold.Sensor] = new HashSet<string>();
                    }

                    // Find the rule that produces this sensor
                    foreach (var rule in rules.Values)
                    {
                        foreach (var action in rule.Actions.OfType<SetValueAction>())
                        {
                            if (action.Key == threshold.Sensor)
                            {
                                _temporalDependencies[threshold.Sensor].Add(rule.Name);
                                _logger.LogDebug(
                                    "Added temporal dependency: {Sensor} depends on rule {Rule}",
                                    threshold.Sensor,
                                    rule.Name
                                );
                            }
                        }
                    }
                    break;
            }

            return dependencies;
        }

        private HashSet<string> GetActionDependencies(
            ActionDefinition action,
            Dictionary<string, RuleDefinition> rules
        )
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
            var sensorPattern = @"\b([a-zA-Z_][a-zA-Z0-9_]*)\b";
            var matches = Regex.Matches(expression, sensorPattern);

            foreach (Match match in matches)
            {
                var potentialSensor = match.Value;
                if (!_mathFunctions.Contains(potentialSensor))
                {
                    sensors.Add(potentialSensor);
                }
            }

            return sensors;
        }
    }
}
