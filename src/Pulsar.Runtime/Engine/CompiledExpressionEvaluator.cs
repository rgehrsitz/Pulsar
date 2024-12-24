using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging;
using Pulsar.RuleDefinition.Models;
using System.IO; // Added
using System.Linq; // Added
using System.Data;  // Added

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
            var transformed = TransformExpression(expression);
            var variableChecks = GenerateVariableChecks(expression);

            var code = @"
using System;
using System.Collections.Generic;
using static System.Math;
using System.Data;
using Pulsar.Runtime.Engine;

public class ExpressionEvaluator
{
    public bool Evaluate(IDictionary<string, double> data)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        try
        {
            " + variableChecks + @"
            
            var context = new SimpleContext();
            foreach (var variable in data)
            {
                context.SetValue(variable.Key, variable.Value);
            }

            var expr = new Expression(""" + transformed.Replace("\"", "\"\"") + @""");
            return expr.Evaluate(context);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($""Error evaluating expression: {ex.Message}"", ex);
        }
    }
}";
            // Parse and compile the code
            var syntaxTree = CSharpSyntaxTree.ParseText(code);
            var assemblyName = Path.GetRandomFileName();
            var references = new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Dictionary<,>).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IDictionary<,>).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(SimpleContext).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Expression).Assembly.Location),
                MetadataReference.CreateFromFile(Assembly.Load("netstandard").Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Data.Common").Location),
                MetadataReference.CreateFromFile(typeof(DataTable).Assembly.Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Data").Location),
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
            var method = type!.GetMethod("Evaluate");

            return data => (bool)method!.Invoke(obj, new object[] { data })!;
        }
        catch (Exception ex)
        {
            throw new ArgumentException(
                $"Error evaluating expression: {ex.Message}",
                ex
            );
        }
    }

    private static string TransformExpression(string expression)
    {
        // Return the expression unchanged - let Expression class handle the transformation
        return expression;
    }

    // A small helper to generate the KeyNotFound checks for each variable
    private static string GenerateVariableChecks(string expression)
    {
        var variables = string.Join(", ", ExtractVariables(expression).Select(v => $"\"{v}\""));
        return $@"// Check for unknown variables first
var variableKeys = new string[] {{ {variables} }};
foreach (var key in variableKeys)
{{
    if (!data.ContainsKey(key))
    {{
        throw new KeyNotFoundException($""Unknown variable '{{key}}' referenced in expression. Please ensure that the variable is defined in the sensor data."");
    }}
}}";
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
