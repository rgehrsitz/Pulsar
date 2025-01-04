using System;
using System.Collections.Generic;
using System.Linq;
using Pulsar.RuleDefinition.Models;
using Pulsar.RuleDefinition.Models.Conditions;
using Pulsar.RuleDefinition.Models.Actions;
using Serilog;

namespace Pulsar.RuleDefinition.Analysis;

public class DependencyAnalyzer
{
    private readonly ILogger _logger;

    public DependencyAnalyzer()
    {
        _logger = Log.ForContext<DependencyAnalyzer>();
    }

    public (List<RuleDefinitionModel> OrderedRules, List<string> CyclicDependencies) AnalyzeAndOrder(
        RuleSetDefinition ruleSet
    )
    {
        _logger.Information(
            "Starting dependency analysis for ruleset {RuleSetVersion} with {RuleCount} rules",
            ruleSet.Version,
            ruleSet.Rules.Count
        );

        // First check for duplicate rule names
        var duplicateRules = ruleSet
            .Rules.GroupBy(r => r.Name)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateRules.Any())
        {
            _logger.Error("Found duplicate rule names: {@DuplicateRules}", duplicateRules);
            return (new List<RuleDefinitionModel>(), new List<string> { "Duplicate rule names found" });
        }

        var rules = ruleSet.Rules.ToDictionary(r => r.Name, r => (RuleDefinitionModel)r);
        var graph = BuildDependencyGraph(rules);

        _logger.Debug("Built dependency graph with {NodeCount} nodes", graph.Count);
        foreach (var (rule, deps) in graph)
        {
            _logger.Debug("Rule {RuleName} depends on: {@Dependencies}", rule, deps);
        }

        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();
        var orderedRules = new List<RuleDefinitionModel>();
        var cyclicDependencies = new List<string>();

        foreach (var rule in rules.Keys)
        {
            if (!visited.Contains(rule))
            {
                _logger.Debug("Checking for cycles starting from rule {RuleName}", rule);
                if (HasCycle(rule, graph, visited, recursionStack, cyclicDependencies))
                {
                    _logger.Warning(
                        "Cyclic dependency detected starting from rule {RuleName}. Cycle: {@CyclicDependencies}",
                        rule,
                        cyclicDependencies
                    );
                }
            }
        }

        if (!cyclicDependencies.Any())
        {
            _logger.Information("No cyclic dependencies found, proceeding with topological sort");

            // If there are no dependencies, maintain original order
            if (!graph.Any(kv => kv.Value.Any()))
            {
                _logger.Information("No dependencies found, maintaining original order");
                orderedRules = ruleSet.Rules.Select(r => (RuleDefinitionModel)r).ToList();
            }
            else
            {
                visited.Clear();
                foreach (var rule in rules.Keys)
                {
                    if (!visited.Contains(rule))
                    {
                        TopologicalSort(rule, graph, visited, rules, orderedRules);
                    }
                }
                _logger.Information("Successfully ordered {RuleCount} rules", orderedRules.Count);
                _logger.Debug(
                    "Rule execution order: {@RuleOrder}",
                    orderedRules.Select(r => r.Name)
                );
            }
        }
        else
        {
            _logger.Error(
                "Found {CycleCount} cyclic dependencies in ruleset",
                cyclicDependencies.Count
            );
            // Format cyclic dependencies as expected by tests
            cyclicDependencies = cyclicDependencies
                .Select(c => $"Cyclic dependency detected: {c}")
                .ToList();
        }

        return (orderedRules, cyclicDependencies);
    }

    private Dictionary<string, HashSet<string>> BuildDependencyGraph(Dictionary<string, RuleDefinitionModel> rules)
    {
        _logger.Debug("Building dependency graph for {RuleCount} rules", rules.Count);
        var graph = new Dictionary<string, HashSet<string>>();

        foreach (var (ruleName, rule) in rules)
        {
            if (!graph.ContainsKey(ruleName))
            {
                graph[ruleName] = new HashSet<string>();
            }

            var dataSources = GetDataSources(rule);
            var outputs = GetOutputs(rule);

            _logger.Debug(
                "Rule {RuleName} - DataSources: {@DataSources}, Outputs: {@Outputs}",
                ruleName,
                dataSources,
                outputs
            );

            foreach (var otherRule in rules.Values)
            {
                var otherOutputs = GetOutputs(otherRule);
                if (dataSources.Intersect(otherOutputs).Any())
                {
                    // If otherRule writes to a sensor that this rule reads from,
                    // then otherRule depends on this rule (not the other way around)
                    if (!graph.ContainsKey(otherRule.Name))
                    {
                        graph[otherRule.Name] = new HashSet<string>();
                    }
                    graph[otherRule.Name].Add(ruleName);
                    _logger.Debug(
                        "Added dependency: {RuleName} depends on {DependencyRule}",
                        otherRule.Name,
                        ruleName
                    );
                }
            }
        }

        return graph;
    }

    private HashSet<string> GetDataSources(RuleDefinitionModel rule)
    {
        var sources = new HashSet<string>();

        if (rule.Conditions != null)
        {
            if (rule.Conditions.All != null)
            {
                foreach (var wrapper in rule.Conditions.All)
                {
                    switch (wrapper.Condition)
                    {
                        case ComparisonConditionDefinition comparison:
                            sources.Add(comparison.DataSource);
                            break;
                        case ThresholdOverTimeConditionDefinition threshold:
                            sources.Add(threshold.DataSource);
                            break;
                        case ExpressionConditionDefinition expression:
                            var parts = expression.Expression.Split(' ');
                            sources.Add(parts[0]); // The first part is always the data source
                            break;
                    }
                }
            }

            if (rule.Conditions.Any != null)
            {
                foreach (var wrapper in rule.Conditions.Any)
                {
                    switch (wrapper.Condition)
                    {
                        case ComparisonConditionDefinition comparison:
                            sources.Add(comparison.DataSource);
                            break;
                        case ThresholdOverTimeConditionDefinition threshold:
                            sources.Add(threshold.DataSource);
                            break;
                        case ExpressionConditionDefinition expression:
                            var parts = expression.Expression.Split(' ');
                            sources.Add(parts[0]); // The first part is always the data source
                            break;
                    }
                }
            }
        }

        return sources;
    }

    private HashSet<string> GetOutputs(RuleDefinitionModel rule)
    {
        var outputs = new HashSet<string>();

        if (rule.Actions != null)
        {
            foreach (var action in rule.Actions)
            {
                if (action.SetValue != null && !string.IsNullOrEmpty(action.SetValue.Key))
                {
                    outputs.Add(action.SetValue.Key);
                }
            }
        }

        return outputs;
    }

    private bool HasCycle(
        string rule,
        Dictionary<string, HashSet<string>> graph,
        HashSet<string> visited,
        HashSet<string> recursionStack,
        List<string> cyclicDependencies
    )
    {
        visited.Add(rule);
        recursionStack.Add(rule);

        _logger.Debug(
            "Checking cycles for rule {RuleName}. RecursionStack: {@RecursionStack}",
            rule,
            recursionStack
        );

        if (graph.TryGetValue(rule, out var dependencies))
        {
            foreach (var dependency in dependencies)
            {
                if (!visited.Contains(dependency))
                {
                    if (HasCycle(dependency, graph, visited, recursionStack, cyclicDependencies))
                    {
                        var cycle = string.Join(" -> ", recursionStack.Reverse());
                        if (!cyclicDependencies.Contains(cycle))
                        {
                            cyclicDependencies.Add(cycle);
                            _logger.Warning("Found cycle: {CyclePath}", cycle);
                        }
                        return true;
                    }
                }
                else if (recursionStack.Contains(dependency))
                {
                    var cycle = string.Join(" -> ", recursionStack.Reverse());
                    if (!cyclicDependencies.Contains(cycle))
                    {
                        cyclicDependencies.Add(cycle);
                        _logger.Warning("Found cycle: {CyclePath}", cycle);
                    }
                    return true;
                }
            }
        }

        recursionStack.Remove(rule);
        return false;
    }

    private void TopologicalSort(
        string rule,
        Dictionary<string, HashSet<string>> graph,
        HashSet<string> visited,
        Dictionary<string, RuleDefinitionModel> rules,
        List<RuleDefinitionModel> orderedRules
    )
    {
        visited.Add(rule);

        if (graph.TryGetValue(rule, out var dependencies))
        {
            foreach (var dependency in dependencies)
            {
                if (!visited.Contains(dependency))
                {
                    TopologicalSort(dependency, graph, visited, rules, orderedRules);
                }
            }
        }

        // Insert at the beginning instead of adding to the end
        orderedRules.Insert(0, rules[rule]);
        _logger.Debug("Added rule {RuleName} to ordered list", rule);
    }
}
