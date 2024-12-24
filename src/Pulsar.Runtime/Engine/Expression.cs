using System;
using System.Data;
using System.Text.RegularExpressions;

namespace Pulsar.Runtime.Engine;

public class Expression
{
    private readonly string _expression;
    private readonly DataTable _table;

    public Expression(string expression)
    {
        _expression = TransformExpression(expression);
        _table = new DataTable();
        
        // Enable expressions on the DataTable
        _table.Columns.Add(new DataColumn("result", typeof(double)) { Expression = "0" });
        
        // Add placeholder row for function results
        var row = _table.NewRow();
        _table.Rows.Add(row);

        // Add function columns with default values to prevent DBNull
        var functions = new[]
        {
            "ABS", "ROUND", "FLOOR", "CEILING", "MAX", "MIN"
        };

        foreach (var func in functions)
        {
            _table.Columns.Add(new DataColumn(func, typeof(double)) { DefaultValue = 0.0 });
        }
    }

    public bool Evaluate(SimpleContext context)
    {
        try
        {
            // Replace variables with their values
            var evaluatedExpression = _expression;
            foreach (var variable in context.GetVariables())
            {
                evaluatedExpression = evaluatedExpression.Replace(
                    variable.Key,
                    variable.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
                );
            }

            Console.WriteLine($"Evaluating expression: {evaluatedExpression}");

            // Compute the result using DataTable
            var result = _table.Compute(evaluatedExpression, string.Empty);

            // Handle DBNull result
            if (result is DBNull)
                return false;

            Console.WriteLine($"Result: {result}");

            // Handle both boolean and numeric results
            return result switch
            {
                bool b => b,
                int i => i != 0,
                double d => Math.Abs(d) > double.Epsilon,
                decimal m => m != 0m,
                _ => Convert.ToBoolean(result)
            };
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Error evaluating expression: {ex.Message}", ex);
        }
    }

    private static string TransformExpression(string expr)
    {
        // Handle special cases for boolean expressions
        expr = Regex.Replace(expr, @"(\d+|\)|\])\s*([<>]=?|==?)\s*(\d+)", "$1 $2 $3");

        // Make equals operator SQL-compatible
        expr = expr.Replace("==", "=");

        // Transform logical operators to SQL-style AND/OR
        expr = expr.Replace("&&", "AND")
                  .Replace("||", "OR");

        // Handle math functions
        expr = expr.Replace("Abs(", "ABS(")
            .Replace("Round(", "ROUND(")
            .Replace("Floor(", "FLOOR(")
            .Replace("Ceiling(", "CEILING(");

        // Handle Max/Min using IIF
        expr = Regex.Replace(expr, @"Max\(([^,]+),([^)]+)\)", "IIF($1 > $2, $1, $2)");
        expr = Regex.Replace(expr, @"Min\(([^,]+),([^)]+)\)", "IIF($1 < $2, $1, $2)");

        Console.WriteLine($"Transformed expression: {expr}");

        return expr;
    }
}
