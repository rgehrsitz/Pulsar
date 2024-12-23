using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging;
using Pulsar.RuleDefinition.Models;

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
            expr => CompileExpression(expr)
        );

        try
        {
            return await Task.Run(() => compiledExpression(sensorData));
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(
                "Missing variable during expression evaluation: {Message}",
                ex.Message
            );
            return false; // Log and skip instead of throwing
        }
        catch (Exception ex)
        {
            _logger.LogError(
                "Error evaluating expression {Expression}: {Message}",
                expressionCondition.Expression,
                ex.Message
            );
            return false; // Log and skip
        }
    }

    private static Func<IDictionary<string, double>, bool> CompileExpression(string expression)
    {
        try
        {
            // Create the method signature
            var code = $$"""
                using System;
                using System.Collections.Generic;
                using static System.Math;

                public class ExpressionEvaluator
                {
                    public async Task<bool> EvaluateAsync(IDictionary<string, double> data)
                    {
                        if (data == null)
                        {
                            throw new ArgumentNullException(nameof(data));
                        }

                        try
                        {
                            // Check for unknown variables first
                            foreach (var key in new[] { {{string.Join(
                    ", ",
                    ExtractVariables(expression).Select(v => $"\"{v}\"")
                )}} })
                            {
                                if (!data.ContainsKey(key))
                                {
                                    throw new KeyNotFoundException($"Unknown variable '{key}' referenced in expression. Please ensure that the variable is defined in the sensor data.");
                                }
                            }

                            var result = await Task.Run(() =>
                            {
                                var context = new SimpleContext();
                                foreach (var variable in data)
                                {
                                    context.SetValue(variable.Key, variable.Value);
                                }

                                var expr = new Expression(TransformExpression(expression));
                                var value = expr.Evaluate(context);

                                if (value is bool boolValue)
                                {
                                    return boolValue;
                                }

                                throw new ArgumentException($"Expression '{expression}' must evaluate to a boolean value");
                            });

                            return result;
                        }
                        catch (KeyNotFoundException ex)
                        {
                            throw ex;
                        }
                        catch (Exception ex)
                        {
                            throw new InvalidOperationException($"Error evaluating expression: {ex.Message}", ex);
                        }
                    }
                }
                """;

            // Parse and compile the code
            var syntaxTree = CSharpSyntaxTree.ParseText(code);
            var assemblyName = Path.GetRandomFileName();
            var references = new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Dictionary<,>).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IDictionary<,>).Assembly.Location),
                MetadataReference.CreateFromFile(Assembly.Load("netstandard").Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
            };

            var compilation = CSharpCompilation.Create(
                assemblyName,
                new[] { syntaxTree },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );

            using var ms = new MemoryStream();
            var result = compilation.Emit(ms);

            if (!result.Success)
            {
                var failures = result.Diagnostics.Where(diagnostic =>
                    diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error
                );

                var errorMessage = string.Join(
                    Environment.NewLine,
                    failures.Select(f => $"{f.Id}: {f.GetMessage()}")
                );

                throw new ArgumentException(
                    $"Invalid expression: {expression}. Compilation errors: {errorMessage}"
                );
            }

            ms.Seek(0, SeekOrigin.Begin);
            var assembly = Assembly.Load(ms.ToArray());
            var type = assembly.GetType("ExpressionEvaluator");
            var obj = Activator.CreateInstance(type!);
            var method = type!.GetMethod("EvaluateAsync");

            return data => (bool)method!.Invoke(obj, new object[] { data })!;
        }
        catch (Exception ex)
        {
            throw new ArgumentException(
                $"Failed to compile expression: {expression}. Error: {ex.Message}",
                ex
            );
        }
    }

    private static string TransformExpression(string expression)
    {
        // Replace sensor data references with dictionary lookups
        var transformed = expression;

        // Replace sensor names with dictionary lookups
        transformed = System.Text.RegularExpressions.Regex.Replace(
            transformed,
            @"\b([a-zA-Z_][a-zA-Z0-9_]*)\b(?!\s*[({])",
            match =>
                match.Value switch
                {
                    "data" => "data",
                    _ => $"data[\"{match.Value}\"]",
                }
        );

        return transformed;
    }

    private static IEnumerable<string> ExtractVariables(string expression)
    {
        // Simple regex to extract variable names (this could be improved with a proper parser)
        var matches = System.Text.RegularExpressions.Regex.Matches(
            expression,
            @"\b([a-zA-Z_][a-zA-Z0-9_]*)\b(?!\s*[({])"
        );

        return matches.Select(m => m.Value).Where(v => !IsReservedWord(v)).Distinct();
    }

    private static bool IsReservedWord(string word) =>
        word switch
        {
            "data" or "return" or "true" or "false" or "null" => true,
            "Abs" or "Round" or "Floor" or "Ceiling" or "Max" or "Min" or "Pow" or "Sqrt" => true,
            _ => false,
        };
}
