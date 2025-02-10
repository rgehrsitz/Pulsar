// File: Pulsar.Compiler/DependencyAnalyzer.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Pulsar.Compiler.Models;

namespace Pulsar.Compiler.Analysis
{
    public class DependencyAnalyzer
    {
        // Maintained from original implementation
        private Dictionary<string, RuleDefinition> _outputs = new();

        public List<RuleDefinition> AnalyzeDependencies(List<RuleDefinition> rules)
        {
            // Clear previous outputs to ensure clean state
            _outputs.Clear();

            // Build dependency graph
            var graph = BuildDependencyGraph(rules);

            // Perform topological sort
            return TopologicalSort(graph);
        }

        private Dictionary<RuleDefinition, List<RuleDefinition>> BuildDependencyGraph(
            List<RuleDefinition> rules)
        {
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
                        Debug.WriteLine($"Added dependency edge from {dependencyRule.Name} to {rule.Name}");
                    }
                }
            }

            return graph;
        }

        private List<string> GetDependencies(RuleDefinition rule)
        {
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
            string[] mathFunctions = {
                "Math", "Abs", "Max", "Min", "Round",
                "Floor", "Ceiling", "Sqrt",
                "Sin", "Cos", "Tan"
            };
            return mathFunctions.Contains(token);
        }

        private List<RuleDefinition> TopologicalSort(
            Dictionary<RuleDefinition, List<RuleDefinition>> graph)
        {
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
            List<RuleDefinition> sorted)
        {
            Debug.WriteLine($"Visiting {rule.Name}");

            if (visiting.Contains(rule))
            {
                throw new InvalidOperationException(
                    $"Cycle detected involving rule '{rule.Name}'!"
                );
            }

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