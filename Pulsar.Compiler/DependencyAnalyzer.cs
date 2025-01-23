// File: Pulsar.Compiler/DependencyAnalyzer.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Pulsar.Compiler.Models;

namespace Pulsar.Compiler.Analysis
{
    public class DependencyAnalyzer
    {
        private readonly Dictionary<string, RuleDefinition> _outputs = new();

        public List<RuleDefinition> AnalyzeDependencies(List<RuleDefinition> rules)
        {
            // Build a dependency graph
            var graph = BuildDependencyGraph(rules);

            // Perform topological sort
            var sortedRules = TopologicalSort(graph);

            return sortedRules;
        }

        private Dictionary<RuleDefinition, List<RuleDefinition>> BuildDependencyGraph(
            List<RuleDefinition> rules
        )
        {
            var graph = new Dictionary<RuleDefinition, List<RuleDefinition>>();
            _outputs.Clear(); // Clear previous outputs

            // Initialize empty lists for all rules
            foreach (var rule in rules)
            {
                graph[rule] = new List<RuleDefinition>();
            }

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

            // Build dependencies
            foreach (var rule in rules)
            {
                var dependencies = GetDependencies(rule);
                foreach (var dependency in dependencies)
                {
                    if (graph.ContainsKey(dependency))
                    {
                        graph[rule].Add(dependency);
                    }
                }
            }

            return graph;
        }

        private HashSet<RuleDefinition> GetDependencies(RuleDefinition rule)
        {
            var dependencies = new HashSet<RuleDefinition>();

            if (rule.Conditions != null)
            {
                // Check All conditions
                foreach (var condition in rule.Conditions.All)
                {
                    AddConditionDependencies(condition, dependencies);
                }

                // Check Any conditions
                foreach (var condition in rule.Conditions.Any)
                {
                    AddConditionDependencies(condition, dependencies);
                }
            }

            return dependencies;
        }

        private void AddConditionDependencies(ConditionDefinition condition, HashSet<RuleDefinition> dependencies)
        {
            switch (condition)
            {
                case ComparisonCondition comparison:
                    if (_outputs.TryGetValue(comparison.Sensor, out var outputRule))
                    {
                        dependencies.Add(outputRule);
                    }
                    break;

                case ThresholdOverTimeCondition threshold:
                    if (_outputs.TryGetValue(threshold.Sensor, out var thresholdRule))
                    {
                        dependencies.Add(thresholdRule);
                    }
                    break;

                case ConditionGroup group:
                    foreach (var subCondition in group.All.Concat(group.Any))
                    {
                        AddConditionDependencies(subCondition, dependencies);
                    }
                    break;
            }
        }

        private List<RuleDefinition> TopologicalSort(Dictionary<RuleDefinition, List<RuleDefinition>> graph)
        {
            var visited = new HashSet<RuleDefinition>();
            var sorted = new List<RuleDefinition>();
            var visiting = new HashSet<RuleDefinition>();

            foreach (var rule in graph.Keys)
            {
                if (!visited.Contains(rule))
                {
                    Visit(rule, graph, visited, visiting, sorted);
                }
            }

            sorted.Reverse();
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
                throw new InvalidOperationException("Circular dependency detected");
            }

            if (visited.Contains(rule))
            {
                return;
            }

            visiting.Add(rule);

            foreach (var dependency in graph[rule])
            {
                Visit(dependency, graph, visited, visiting, sorted);
            }

            visiting.Remove(rule);
            visited.Add(rule);
            sorted.Add(rule);
        }
    }
}
