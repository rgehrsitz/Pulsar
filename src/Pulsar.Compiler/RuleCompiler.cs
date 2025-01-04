using System;
using System.Collections.Generic;
using System.Linq;
using Pulsar.Models;
using Pulsar.Models.Actions;
using Pulsar.RuleDefinition.Models;
using Pulsar.RuleDefinition.Models.Conditions;
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

        // Map rules to their outputs for dependency analysis
        var outputMap = rulesList.ToDictionary(
            r => r.Name,
            r => (Rule: r, Outputs: ExtractOutputSensors(r))
        );

        // Build dependencies based on input/output relationships
        var dependencies = new Dictionary<string, HashSet<string>>();
        var ruleLayers = new Dictionary<Rule, int>();

        foreach (var rule in rulesList)
        {
            var inputs = ExtractInputSensors(rule);
            _logger.Debug("Analyzing rule {Rule} - reads: {Inputs}", rule.Name, string.Join(", ", inputs));

            foreach (var otherRule in rulesList)
            {
                if (otherRule == rule) continue;

                var outputs = ExtractOutputSensors(otherRule);
                if (outputs.Overlaps(inputs))
                {
                    // Current rule depends on other rule's outputs
                    if (!dependencies.TryGetValue(rule.Name, out var deps))
                    {
                        deps = new HashSet<string>();
                        dependencies[rule.Name] = deps;
                    }
                    deps.Add(otherRule.Name);
                    _logger.Debug("Added dependency: {Consumer} depends on {Producer}",
                        rule.Name, otherRule.Name);
                }
            }
        }

        // Assign layers based on dependencies
        foreach (var rule in rulesList)
        {
            AssignLayer(rule, dependencies, ruleLayers, outputMap);
        }

        // Create ordered compiled rule set
        var compiledRules = rulesList
            .Select(rule => new CompiledRule(
                rule,
                ruleLayers[rule],
                dependencies.GetValueOrDefault(rule.Name, new()),
                ExtractInputSensors(rule),
                ExtractOutputSensors(rule)))
            .OrderBy(r => r.Layer)
            .ToList();

        var maxLayer = compiledRules.Max(r => r.Layer);

        _logger.Information(
            "Compiled {RuleCount} rules into {LayerCount} layers",
            compiledRules.Count,
            maxLayer + 1
        );

        var compiledRuleSet = new CompiledRuleSet(
            compiledRules,
            maxLayer + 1,
            compiledRules.SelectMany(r => r.InputSensors).ToHashSet(),
            compiledRules.SelectMany(r => r.OutputSensors).ToHashSet()
        );

        return (compiledRuleSet, _codeGenerator.GenerateCode(compiledRuleSet));
    }

    private void AssignLayer(
        Rule rule,
        Dictionary<string, HashSet<string>> dependencies,
        Dictionary<Rule, int> ruleLayers,
        Dictionary<string, (Rule Rule, HashSet<string> Outputs)> outputMap
    )
    {
        if (ruleLayers.ContainsKey(rule))
            return;

        int layer = 0;
        if (dependencies.TryGetValue(rule.Name, out var deps))
        {
            foreach (var dep in deps)
            {
                var producer = outputMap[dep].Rule;
                AssignLayer(producer, dependencies, ruleLayers, outputMap);
                layer = Math.Max(layer, ruleLayers[producer] + 1);
            }
        }

        ruleLayers[rule] = layer;
        _logger.Debug("Assigned rule {RuleName} to layer {Layer}", rule.Name, layer);
    }

    private IEnumerable<string> ExtractSensorNames(Condition condition)
    {
        switch (condition)
        {
            case ComparisonCondition comparison:
                yield return comparison.DataSource;
                break;
            case ThresholdOverTimeCondition threshold:
                yield return threshold.DataSource;
                break;
            case ExpressionCondition expr:
                // Basic expression parsing - extract sensor names from expression
                var parts = expr.Expression.Split(new[] { ' ', '(', ')', '+', '-', '*', '/', '>', '<', '=', '&', '|', ',' },
                    StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    if (!double.TryParse(part, out _)) // If not a number, treat as sensor name
                    {
                        yield return part;
                    }
                }
                break;
        }
    }

    private HashSet<string> ExtractInputSensors(Rule rule)
    {
        var sensors = new HashSet<string>();
        if (rule.Conditions.All != null)
        {
            foreach (var condition in rule.Conditions.All)
            {
                foreach (var sensor in ExtractSensorNames(condition.Condition))
                {
                    sensors.Add(sensor);
                }
            }
        }
        if (rule.Conditions.Any != null)
        {
            foreach (var condition in rule.Conditions.Any)
            {
                foreach (var sensor in ExtractSensorNames(condition.Condition))
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

        // Build map of outputs to their producing rules
        foreach (var rule in rules)
        {
            foreach (var sensor in ExtractOutputSensors(rule))
            {
                outputs[sensor] = rule.Name;
            }
        }

        // Build dependencies from inputs to outputs
        foreach (var rule in rules)
        {
            inDegree[rule.Name] = 0;
            var inputs = ExtractInputSensors(rule);

            foreach (var input in inputs)
            {
                if (outputs.TryGetValue(input, out var producer))
                {
                    if (!dependencies.TryGetValue(producer, out var deps))
                    {
                        deps = new HashSet<string>();
                        dependencies[producer] = deps;
                    }
                    if (deps.Add(rule.Name))
                    {
                        inDegree[rule.Name]++;
                    }
                }
            }
        }

        return (dependencies, inDegree);
    }
}
