using System;
using System.Collections.Generic;
using System.Linq;
using Pulsar.RuleDefinition.Models;
using Pulsar.RuleDefinition.Validation;

namespace Pulsar.RuleDefinition.Analysis;

public class DependencyAnalyzer
{
    private readonly ExpressionValidator _expressionValidator;

    public DependencyAnalyzer()
    {
        _expressionValidator = new ExpressionValidator();
    }

    public (List<Rule> orderedRules, List<string> cyclicDependencies) AnalyzeAndOrder(RuleSetDefinition ruleSet)
    {
        var graph = BuildDependencyGraph(ruleSet.Rules);
        var cyclicDependencies = new List<string>();

        // Check for cyclic dependencies
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();

        foreach (var rule in ruleSet.Rules)
        {
            if (HasCyclicDependency(rule.Name, graph, visited, recursionStack))
            {
                cyclicDependencies.Add($"Cyclic dependency detected involving rule '{rule.Name}'");
            }
        }

        if (cyclicDependencies.Any())
        {
            return (ruleSet.Rules, cyclicDependencies);
        }

        // If no dependencies exist, return the original order
        if (!graph.Values.Any(deps => deps.Any()))
        {
            return (ruleSet.Rules, cyclicDependencies);
        }

        // Perform topological sort
        var orderedRules = TopologicalSort(ruleSet.Rules, graph);
        return (orderedRules, cyclicDependencies);
    }

    private Dictionary<string, HashSet<string>> BuildDependencyGraph(List<Rule> rules)
    {
        var graph = rules.ToDictionary(r => r.Name, _ => new HashSet<string>());
        var rulesByOutput = rules.ToDictionary(
            r => r.Name,
            r => r.Actions
                .SelectMany(a => a.SetValue.Where(kv => kv.Key == "key").Select(kv => kv.Value?.ToString()))
                .Where(k => k != null)
                .ToHashSet()!
        );

        // Build the dependency graph
        foreach (var rule in rules)
        {
            var dataSources = ExtractDataSources(rule);
            foreach (var source in dataSources)
            {
                // Find which rule produces this data source
                var producer = rules.FirstOrDefault(r => rulesByOutput[r.Name].Contains(source));
                if (producer != null && producer.Name != rule.Name)  // Avoid self-dependencies
                {
                    graph[producer.Name].Add(rule.Name);  // Producer must run before consumer
                }
            }
        }

        return graph;
    }

    private HashSet<string> ExtractDataSources(Rule rule)
    {
        var dataSources = new HashSet<string>();

        void ProcessConditions(List<Condition>? conditions)
        {
            if (conditions == null) return;

            foreach (var condition in conditions)
            {
                switch (condition)
                {
                    case ComparisonCondition comp:
                        dataSources.Add(comp.DataSource);
                        break;

                    case ThresholdOverTimeCondition threshold:
                        dataSources.Add(threshold.DataSource);
                        break;

                    case ExpressionCondition expr:
                        var (_, sources, _) = _expressionValidator.ValidateExpression(expr.Expression);
                        dataSources.UnionWith(sources);
                        break;
                }
            }
        }

        ProcessConditions(rule.Conditions.All);
        ProcessConditions(rule.Conditions.Any);

        return dataSources;
    }

    private bool HasCyclicDependency(
        string ruleName,
        Dictionary<string, HashSet<string>> graph,
        HashSet<string> visited,
        HashSet<string> recursionStack)
    {
        if (recursionStack.Contains(ruleName))
            return true;

        if (visited.Contains(ruleName))
            return false;

        visited.Add(ruleName);
        recursionStack.Add(ruleName);

        if (graph.ContainsKey(ruleName))
        {
            foreach (var dependency in graph[ruleName])
            {
                if (HasCyclicDependency(dependency, graph, visited, recursionStack))
                {
                    return true;
                }
            }
        }

        recursionStack.Remove(ruleName);
        return false;
    }

    private List<Rule> TopologicalSort(List<Rule> rules, Dictionary<string, HashSet<string>> graph)
    {
        var visited = new HashSet<string>();
        var sorted = new List<Rule>();

        void Visit(string ruleName)
        {
            if (visited.Contains(ruleName))
                return;

            visited.Add(ruleName);

            if (graph.ContainsKey(ruleName))
            {
                foreach (var dependency in graph[ruleName])
                {
                    Visit(dependency);
                }
            }

            sorted.Add(rules.First(r => r.Name == ruleName));
        }

        // Visit rules in original order to maintain ordering when no dependencies exist
        foreach (var rule in rules.AsEnumerable().Reverse())  // Start from the last rule
        {
            if (!visited.Contains(rule.Name))
            {
                Visit(rule.Name);
            }
        }

        sorted.Reverse();  // Reverse to get the correct order
        return sorted;
    }
}
