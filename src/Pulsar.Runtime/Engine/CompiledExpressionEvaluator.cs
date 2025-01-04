using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging;
using Pulsar.RuleDefinition.Models;
using Pulsar.RuleDefinition.Models.Conditions;

namespace Pulsar.Runtime.Engine;

/// <summary>
/// Evaluates expression-based conditions using compiled C# code
/// </summary>
public class CompiledExpressionEvaluator : IConditionEvaluator
{
    private readonly ConcurrentDictionary<
        string,
        Func<IDictionary<string, double>, bool>
    > _compiledExpressions = new();
    private readonly ILogger _logger;

    public CompiledExpressionEvaluator(ILogger logger)
    {
        _logger = logger;
    }

    public async Task<bool> EvaluateAsync(
        Condition condition,
        IDictionary<string, double> sensorData
    )
    {
        if (condition is not ExpressionCondition expressionCondition)
        {
            throw new ArgumentException(
                $"Expected ExpressionCondition but got {condition.GetType().Name}"
            );
        }

        var compiledExpression = _compiledExpressions.GetOrAdd(
            expressionCondition.Expression,
            expr =>
                (data) =>
                {
                    var context = new SimpleContext();
                    foreach (var kv in data)
                        context.SetValue(kv.Key, kv.Value);

                    var e = new Expression(expr);
                    return e.Evaluate(context);
                }
        );

        try
        {
            return await Task.Run(() => compiledExpression(sensorData));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                "Error evaluating expression {Expression}: {Message}",
                expressionCondition.Expression,
                ex.Message
            );
            throw new ArgumentException($"Error evaluating expression: {ex.Message}", ex);
        }
    }
}
