using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pulsar.RuleDefinition.Models.Conditions;

namespace Pulsar.Runtime.Engine;

/// <summary>
/// Evaluates conditions using compiled expressions
/// </summary>
public class CompiledExpressionEvaluator : IConditionEvaluator
{
    private readonly ConcurrentDictionary<
        string,
        Func<IDictionary<string, object>, bool>
    > _compiledExpressions = new();
    private readonly ILogger _logger;

    public CompiledExpressionEvaluator(ILogger logger)
    {
        _logger = logger;
    }

    public async Task<bool> EvaluateAsync(
        ConditionWrapperDefinition condition,
        IDictionary<string, object> data
    )
    {
        if (condition.Expression is not ExpressionConditionDefinition expressionCondition)
        {
            throw new ArgumentException(
                $"Expected ExpressionConditionDefinition but got {condition.Expression?.GetType().Name ?? "null"}"
            );
        }

        var expression = expressionCondition.Expression;
        var evaluator =
            _compiledExpressions.GetOrAdd(
                expression,
                expr =>
                {
                    try
                    {
                        return CompileExpression(expr);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to compile expression: {Expression}", expr);
                        throw;
                    }
                }
            );

        try
        {
            return evaluator(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to evaluate expression: {Expression} with data: {Data}",
                expression,
                string.Join(", ", data.Select(kvp => $"{kvp.Key}={kvp.Value}"))
            );
            throw;
        }
    }

    private Func<IDictionary<string, object>, bool> CompileExpression(string expression)
    {
        // Implement CompileExpression method to handle string values
        // This method should return a Func that takes a dictionary of string-object pairs and returns a boolean
        // The implementation of this method is not provided in the given code edit
    }
}
