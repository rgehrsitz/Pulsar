// File: Pulsar.Compiler/DependencyAnalyzer.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Pulsar.Compiler.Models;
using Serilog;

namespace Pulsar.Compiler.Analysis
{
    public class DependencyAnalyzer
    {
        private readonly ILogger _logger;
        private Dictionary<string, RuleDefinition> _outputs = new();
        private readonly int _maxDependencyDepth;
        private readonly Dictionary<string, HashSet<string>> _temporalDependencies = new();

        public DependencyAnalyzer(int maxDependencyDepth = 10)
        {
            _logger = LoggingConfig.GetLogger();
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
            var graph = BuildDependencyGraph(rules);

            // Check for circular dependencies
            var cycles = FindCircularDependencies(graph);
            if (cycles.Any())
            {
                result.IsValid = false;
                result.CircularDependencies = cycles;
                foreach (var cycle in cycles)
                {
                    _logger.Error("Circular dependency detected: {Path}", string.Join(" -> ", cycle));
                }
            }

            // Check dependency depths
            var deepChains = FindDeepDependencyChains(graph);
            if (deepChains.Any())
            {
                result.DeepDependencyChains = deepChains;
                foreach (var chain in deepChains)
                {
                    _logger.Warning("Deep dependency chain detected: {Path}", string.Join(" -> ", chain));
                }
            }

            // Calculate complexity scores
            result.RuleComplexityScores = CalculateRuleComplexity(rules, graph);
            
            // Track temporal dependencies
            result.TemporalDependencies = _temporalDependencies;

            return result;
        }

        private List<List<string>> FindCircularDependencies(Dictionary<RuleDefinition, List<RuleDefinition>> graph)
        {
            var cycles = new List<List<string>>();
            var visited = new HashSet<RuleDefinition>();
            var path = new List<RuleDefinition>();

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
            RuleDefinition current,
            Dictionary<RuleDefinition, List<RuleDefinition>> graph,
            HashSet<RuleDefinition> visited,
            List<RuleDefinition> path,
            List<List<string>> cycles)
        {
            if (path.Contains(current))
            {
                var cycleStart = path.IndexOf(current);
                var cycle = path.Skip(cycleStart)
                               .Select(r => r.Name)
                               .Concat(new[] { current.Name })
                               .ToList();
                cycles.Add(cycle);
                return;
            }

            if (visited.Contains(current))
                return;

            visited.Add(current);
            path.Add(current);

            foreach (var dependent in graph[current])
            {
                DetectCycle(dependent, graph, visited, path, cycles);
            }

            path.RemoveAt(path.Count - 1);
        }

        private List<List<string>> FindDeepDependencyChains(Dictionary<RuleDefinition, List<RuleDefinition>> graph)
        {
            var deepChains = new List<List<string>>();
            var visited = new HashSet<RuleDefinition>();
            var path = new List<RuleDefinition>();

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
            RuleDefinition current,
            Dictionary<RuleDefinition, List<RuleDefinition>> graph,
            HashSet<RuleDefinition> visited,
            List<RuleDefinition> path,
            List<List<string>> deepChains)
        {
            path.Add(current);

            if (path.Count > _maxDependencyDepth)
            {
                deepChains.Add(path.Select(r => r.Name).ToList());
            }

            if (!visited.Contains(current))
            {
                visited.Add(current);
                foreach (var dependent in graph[current])
                {
                    FindLongPaths(dependent, graph, visited, path, deepChains);
                }
                visited.Remove(current);
            }

            path.RemoveAt(path.Count - 1);
        }

        private Dictionary<string, int> CalculateRuleComplexity(
            List<RuleDefinition> rules,
            Dictionary<RuleDefinition, List<RuleDefinition>> graph)
        {
            var scores = new Dictionary<string, int>();

            foreach (var rule in rules)
            {
                var score = 0;
                
                // Add points for conditions
                if (rule.Conditions != null)
                {
                    score += (rule.Conditions.All?.Count ?? 0) * 2;
                    score += (rule.Conditions.Any?.Count ?? 0) * 3;
                }

                // Add points for actions
                score += rule.Actions.Count;

                // Add points for temporal conditions
                score += rule.Conditions?.All?.OfType<ThresholdOverTimeCondition>().Count() ?? 0 * 5;
                score += rule.Conditions?.Any?.OfType<ThresholdOverTimeCondition>().Count() ?? 0 * 5;

                // Add points for dependency depth
                var depthScore = CalculateDependencyDepth(rule, graph);
                score += depthScore * 3;

                scores[rule.Name] = score;
            }

            return scores;
        }

        private int CalculateDependencyDepth(
            RuleDefinition rule,
            Dictionary<RuleDefinition, List<RuleDefinition>> graph)
        {
            var visited = new HashSet<RuleDefinition>();
            var depth = 0;
            var queue = new Queue<(RuleDefinition Rule, int Depth)>();
            queue.Enqueue((rule, 0));

            while (queue.Count > 0)
            {
                var (current, currentDepth) = queue.Dequeue();
                if (!visited.Contains(current))
                {
                    visited.Add(current);
                    depth = Math.Max(depth, currentDepth);

                    foreach (var dependent in graph[current])
                    {
                        queue.Enqueue((dependent, currentDepth + 1));
                    }
                }
            }

            return depth;
        }

        private Dictionary<RuleDefinition, List<RuleDefinition>> BuildDependencyGraph(
            List<RuleDefinition> rules
        )
        {
            _logger.Debug("Building dependency graph");
            var graph = new Dictionary<RuleDefinition, List<RuleDefinition>>();

            // Collect outputs from each rule
            foreach (var rule in rules)
            {
                foreach (var action in rule.Actions)
                {
                    if (action is SetValueAction setValueAction)
                    {
                        _outputs[setValueAction.Key] = rule;
                    }
                }
            }

            // Initialize empty lists for all rules
            foreach (var rule in rules)
            {
                graph[rule] = new List<RuleDefinition>();
            }

            // Build dependencies
            foreach (var rule in rules)
            {
                var dependencies = GetDependencies(rule);
                foreach (var dependency in dependencies)
                {
                    if (_outputs.TryGetValue(dependency, out var dependencyRule))
                    {
                        graph[dependencyRule].Add(rule);
                        Debug.WriteLine(
                            $"Added dependency edge from {dependencyRule.Name} to {rule.Name}"
                        );
                    }
                }
            }

            return graph;
        }

        private List<string> GetDependencies(RuleDefinition rule)
        {
            _logger.Debug("Getting dependencies for rule: {RuleName}", rule.Name);
            var dependencies = new List<string>();

            // Collect dependencies from conditions
            void AddConditionDependencies(ConditionDefinition condition)
            {
                switch (condition)
                {
                    case ComparisonCondition comp:
                        dependencies.Add(comp.Sensor);
                        break;
                    case ExpressionCondition expr:
                        // Extract sensors from expression using regex
                        ExtractSensorsFromExpression(expr.Expression, dependencies);
                        break;
                    case ThresholdOverTimeCondition temporal:
                        dependencies.Add(temporal.Sensor);
                        _temporalDependencies[rule.Name] = new HashSet<string> { temporal.Sensor };
                        break;
                    case ConditionGroup group:
                        group.All?.ForEach(AddConditionDependencies);
                        group.Any?.ForEach(AddConditionDependencies);
                        break;
                }
            }

            // Process conditions
            if (rule.Conditions?.All != null)
            {
                rule.Conditions.All.ForEach(AddConditionDependencies);
            }

            if (rule.Conditions?.Any != null)
            {
                rule.Conditions.Any.ForEach(AddConditionDependencies);
            }

            return dependencies.Distinct().ToList();
        }

        private void ExtractSensorsFromExpression(string expression, List<string> dependencies)
        {
            _logger.Debug("Extracting sensors from expression: {Expression}", expression);
            if (string.IsNullOrWhiteSpace(expression))
                return;

            // Basic sensor extraction using regex
            var sensorPattern = @"\b([a-zA-Z_][a-zA-Z0-9_]*)\b";
            var matches = System.Text.RegularExpressions.Regex.Matches(expression, sensorPattern);

            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                var potentialSensor = match.Value;
                // Exclude known math functions and keywords
                if (!IsMathFunction(potentialSensor))
                {
                    dependencies.Add(potentialSensor);
                }
            }
        }

        private bool IsMathFunction(string token)
        {
            // List of known math functions to exclude from sensor extraction
            string[] mathFunctions =
            {
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

        private List<RuleDefinition> TopologicalSort(
            Dictionary<RuleDefinition, List<RuleDefinition>> graph
        )
        {
            _logger.Debug("Performing topological sort on {Count} nodes", graph.Count);
            var sorted = new List<RuleDefinition>();
            var visited = new HashSet<RuleDefinition>();
            var visiting = new HashSet<RuleDefinition>();

            // Find nodes that have incoming edges (are depended upon)
            var hasIncomingEdges = new HashSet<RuleDefinition>();
            foreach (var kvp in graph)
            {
                foreach (var dependent in kvp.Value)
                {
                    hasIncomingEdges.Add(dependent);
                }
            }

            Debug.WriteLine("\nProcessing nodes with no incoming edges first (base nodes):");
            // First process nodes with no incoming edges (nothing depends on them)
            foreach (var rule in graph.Keys)
            {
                if (!hasIncomingEdges.Contains(rule))
                {
                    Debug.WriteLine($"Starting with base node: {rule.Name}");
                    if (!visited.Contains(rule))
                    {
                        Visit(rule, graph, visited, visiting, sorted);
                    }
                }
            }

            Debug.WriteLine("\nProcessing remaining nodes:");
            // Then process any remaining nodes
            foreach (var rule in graph.Keys)
            {
                if (!visited.Contains(rule))
                {
                    Debug.WriteLine($"Processing remaining node: {rule.Name}");
                    Visit(rule, graph, visited, visiting, sorted);
                }
            }

            return sorted;
        }

        private void Visit(
            RuleDefinition rule,
            Dictionary<RuleDefinition, List<RuleDefinition>> graph,
            HashSet<RuleDefinition> visited,
            HashSet<RuleDefinition> visiting,
            List<RuleDefinition> sorted
        )
        {
            if (visiting.Contains(rule))
            {
                _logger.Error("Cyclic dependency detected involving rule {RuleName}", rule.Name);
                throw new InvalidOperationException(
                    $"Cycle detected involving rule '{rule.Name}'!"
                );
            }

            Debug.WriteLine($"Visiting {rule.Name}");

            if (!visited.Contains(rule))
            {
                visiting.Add(rule);

                // Visit all dependents first
                foreach (var dependent in graph[rule])
                {
                    if (!visited.Contains(dependent))
                    {
                        Visit(dependent, graph, visited, visiting, sorted);
                    }
                }

                visiting.Remove(rule);
                visited.Add(rule);

                if (!sorted.Contains(rule))
                {
                    // If this rule has dependents, insert at beginning
                    // Otherwise append to maintain original order
                    if (graph[rule].Any())
                    {
                        sorted.Insert(0, rule);
                        Debug.WriteLine(
                            $"Added {rule.Name} to start of sorted list (has dependents)"
                        );
                    }
                    else
                    {
                        sorted.Add(rule);
                        Debug.WriteLine($"Added {rule.Name} to end of sorted list (no dependents)");
                    }
                }
            }
        }
    }
}
