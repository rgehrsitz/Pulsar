using System;
using System.Collections.Generic;
using System.Linq;
using Pulsar.Models;
using Pulsar.Models.Actions;
using Pulsar.RuleDefinition.Models;
using Serilog;
using Pulsar.Compiler.CodeGeneration;

namespace Pulsar.Compiler;

/// <summary>
/// Compiles rules by analyzing their dependencies and organizing them into execution layers
/// </summary>
public class RuleCompiler
{
    private readonly ILogger _logger;
    private readonly RuleCodeGenerator _codeGenerator;

    public RuleCompiler(ILogger logger, string? @namespace = null)
    {
        _logger = logger.ForContext<RuleCompiler>();
        _codeGenerator = new RuleCodeGenerator(logger, @namespace ?? "Pulsar.CompiledRules");
    }

    /// <summary>
    /// Compiles a set of rules by analyzing their dependencies and organizing them into layers
    /// </summary>
    public (CompiledRuleSet RuleSet, string GeneratedCode) CompileRules(IEnumerable<Rule> rules)
    {
        var rulesList = rules.ToList();
        if (!rulesList.Any())
        {
            var emptyRuleSet = new CompiledRuleSet(
                Array.Empty<CompiledRule>(),
                0,
                new HashSet<string>(),
                new HashSet<string>()
            );
            return (emptyRuleSet, _codeGenerator.GenerateCode(emptyRuleSet));
        }

        // Build dependency graph
        var (dependencies, inDegree) = BuildDependencyGraph(rulesList);

        // Perform topological sort using Kahn's algorithm
        var layers = new List<List<Rule>>();
        var currentLayer = new List<Rule>();
        var remainingRules = new HashSet<Rule>(rulesList);

        while (remainingRules.Any())
        {
            var independentRules = remainingRules
                .Where(rule => !inDegree.ContainsKey(rule.Name) || inDegree[rule.Name] == 0)
                .ToList();

            if (!independentRules.Any())
            {
                var cycle = FindCycle(dependencies, remainingRules);
                _logger.Error(
                    "Circular dependency detected between rules: {Cycle}",
                    string.Join(" -> ", cycle.Select(r => r.Name))
                );
                throw new InvalidOperationException("Circular dependency detected in rules");
            }

            // Add all independent rules to current layer
            currentLayer.AddRange(independentRules);

            // Remove processed rules and update in-degrees
            foreach (var rule in independentRules)
            {
                remainingRules.Remove(rule);
                if (dependencies.TryGetValue(rule.Name, out var dependents))
                {
                    foreach (var dependent in dependents)
                    {
                        inDegree[dependent]--;
                    }
                }
            }

            // If we've processed all independent rules at this level, start a new layer
            if (currentLayer.Any())
            {
                layers.Add(new List<Rule>(currentLayer));
                currentLayer.Clear();
            }
        }

        // Create compiled rules with layer information
        var compiledRules = new List<CompiledRule>();
        var allInputSensors = new HashSet<string>();
        var allOutputSensors = new HashSet<string>();

        for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
        {
            foreach (var rule in layers[layerIndex])
            {
                var ruleDependencies = new HashSet<string>();
                var inputSensors = ExtractInputSensors(rule);
                var outputSensors = ExtractOutputSensors(rule);

                // Find dependencies by looking up which rules output our input sensors
                foreach (var sensor in inputSensors)
                {
                    foreach (var otherRule in rulesList)
                    {
                        if (otherRule == rule)
                            continue;

                        if (RuleOutputsSensor(otherRule, sensor))
                        {
                            ruleDependencies.Add(otherRule.Name);
                        }
                    }
                }

                allInputSensors.UnionWith(inputSensors);
                allOutputSensors.UnionWith(outputSensors);

                compiledRules.Add(
                    new CompiledRule(
                        rule,
                        layerIndex,
                        ruleDependencies,
                        inputSensors,
                        outputSensors
                    )
                );
            }
        }

        _logger.Information(
            "Compiled {RuleCount} rules into {LayerCount} layers",
            compiledRules.Count,
            layers.Count
        );

        var compiledRuleSet = new CompiledRuleSet(compiledRules, layers.Count, allInputSensors, allOutputSensors);
        var generatedCode = _codeGenerator.GenerateCode(compiledRuleSet);

        return (compiledRuleSet, generatedCode);
    }

    private (
        Dictionary<string, HashSet<string>> Dependencies,
        Dictionary<string, int> InDegree
    ) BuildDependencyGraph(
        IEnumerable<Rule> rules
    )
    {
        var dependencies = new Dictionary<string, HashSet<string>>();
        var inDegree = new Dictionary<string, int>();
        var outputs = new Dictionary<string, string>();

        // First, build a map of which rule produces which outputs
        foreach (var rule in rules)
        {
            foreach (var action in rule.Actions)
            {
                if (action.SetValue != null && !string.IsNullOrEmpty(action.SetValue.Key))
                {
                    if (outputs.TryGetValue(action.SetValue.Key, out var existingRule))
                    {
                        _logger.Warning(
                            "Multiple rules trying to set the same value {Key}: {ExistingRule} and {NewRule}",
                            action.SetValue.Key,
                            existingRule,
                            rule.Name
                        );
                    }
                    outputs[action.SetValue.Key] = rule.Name;
                }
            }
        }

        // Then, analyze each rule's conditions to build the dependency graph
        foreach (var rule in rules)
        {
            inDegree[rule.Name] = 0;
            AnalyzeConditionDependencies(
                rule.Conditions,
                rule.Name,
                outputs,
                dependencies,
                inDegree
            );
        }

        return (dependencies, inDegree);
    }

    private void AnalyzeConditionDependencies(
        ConditionGroup conditions,
        string ruleName,
        Dictionary<string, string> outputs,
        Dictionary<string, HashSet<string>> dependencies,
        Dictionary<string, int> inDegree
    )
    {
        if (conditions.All != null)
        {
            foreach (var condition in conditions.All)
            {
                AddDependencyIfExists(condition, ruleName, outputs, dependencies, inDegree);
            }
        }

        if (conditions.Any != null)
        {
            foreach (var condition in conditions.Any)
            {
                AddDependencyIfExists(condition, ruleName, outputs, dependencies, inDegree);
            }
        }
    }

    private void AddDependencyIfExists(
        Condition condition,
        string dependentRule,
        Dictionary<string, string> outputs,
        Dictionary<string, HashSet<string>> dependencies,
        Dictionary<string, int> inDegree
    )
    {
        var sensorNames = ExtractSensorNames(condition);

        foreach (var sensorName in sensorNames)
        {
            if (outputs.TryGetValue(sensorName, out var producerRule))
            {
                // Add dependency: producerRule -> dependentRule
                if (!dependencies.TryGetValue(producerRule, out var dependents))
                {
                    dependents = new HashSet<string>();
                    dependencies[producerRule] = dependents;
                }
                if (dependents.Add(dependentRule))
                {
                    inDegree[dependentRule]++;
                }
            }
        }
    }

    private IEnumerable<string> ExtractSensorNames(Condition condition)
    {
        if (condition is ComparisonCondition comparison)
        {
            yield return comparison.DataSource;
        }
        else if (condition is ThresholdOverTimeCondition threshold)
        {
            yield return threshold.DataSource;
        }
    }

    private HashSet<string> ExtractInputSensors(Rule rule)
    {
        var sensors = new HashSet<string>();
        if (rule.Conditions.All != null)
        {
            foreach (var condition in rule.Conditions.All)
            {
                foreach (var sensor in ExtractSensorNames(condition))
                {
                    sensors.Add(sensor);
                }
            }
        }
        if (rule.Conditions.Any != null)
        {
            foreach (var condition in rule.Conditions.Any)
            {
                foreach (var sensor in ExtractSensorNames(condition))
                {
                    sensors.Add(sensor);
                }
            }
        }
        return sensors;
    }

    private HashSet<string> ExtractOutputSensors(Rule rule)
    {
        var sensors = new HashSet<string>();
        foreach (var action in rule.Actions)
        {
            if (action.SetValue != null && !string.IsNullOrEmpty(action.SetValue.Key))
            {
                sensors.Add(action.SetValue.Key);
            }
        }
        return sensors;
    }

    private bool RuleOutputsSensor(Rule rule, string sensor)
    {
        return rule.Actions.Any(action => 
            action.SetValue != null && 
            action.SetValue.Key == sensor);
    }

    private IList<Rule> FindCycle(
        Dictionary<string, HashSet<string>> dependencies,
        ISet<Rule> remainingRules
    )
    {
        var visited = new HashSet<string>();
        var path = new HashSet<string>();
        var cycle = new List<Rule>();

        foreach (var rule in remainingRules)
        {
            if (FindCycleDFS(rule.Name, dependencies, visited, path, cycle, remainingRules))
            {
                return cycle;
            }
        }

        return cycle;
    }

    private bool FindCycleDFS(
        string ruleName,
        Dictionary<string, HashSet<string>> dependencies,
        HashSet<string> visited,
        HashSet<string> path,
        List<Rule> cycle,
        ISet<Rule> remainingRules
    )
    {
        if (!visited.Add(ruleName))
        {
            if (path.Contains(ruleName))
            {
                var rule = remainingRules.First(r => r.Name == ruleName);
                cycle.Add(rule);
                return true;
            }
            return false;
        }

        path.Add(ruleName);

        if (dependencies.TryGetValue(ruleName, out var dependents))
        {
            foreach (var dependent in dependents)
            {
                if (FindCycleDFS(dependent, dependencies, visited, path, cycle, remainingRules))
                {
                    var rule = remainingRules.First(r => r.Name == ruleName);
                    cycle.Insert(0, rule);
                    return true;
                }
            }
        }

        path.Remove(ruleName);
        return false;
    }
}
