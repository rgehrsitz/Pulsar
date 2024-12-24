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

        // Add a result column first
        _table.Columns.Add("result", typeof(double));

        // Add a row to hold values
        _table.Rows.Add(_table.NewRow());

        // Register custom functions
        var functions = new Dictionary<string, Func<double[], double>>
        {
            // Remove SQRT and POWER columns
            ["SQRT"] = args => Math.Sqrt(args[0]),
            ["FLOOR"] = args => Math.Floor(args[0]),
            ["POWER"] = args => Math.Pow(args[0], args[1]),
            ["ABS"] = args => Math.Abs(args[0]),
            ["ROUND"] = args => Math.Round(args[0]),
            ["FLOOR"] = args => Math.Floor(args[0]),
            ["CEILING"] = args => Math.Ceiling(args[0]),
            ["MAX"] = args => Math.Max(args[0], args[1]),
            ["MIN"] = args => Math.Min(args[0], args[1]),
        };

        foreach (var func in functions)
        {
            _table.Columns.Add(new DataColumn(func.Key, typeof(double), null));
        }
    }

    public bool Evaluate(SimpleContext context)
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

        Console.WriteLine($"Result: {result}");

        // Handle both boolean and numeric results
        return result switch
        {
            bool b => b,
            int i => i != 0,
            double d => Math.Abs(d) > double.Epsilon,
            decimal m => m != 0m,
            _ => Convert.ToBoolean(result),
        };
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

        // Replace SQRT(...) with ( ... )^0.5
        expr = Regex.Replace(
            expr,
            @"SQRT\s*\(\s*([^()]+)\s*\)",
            @"($1)^0.5",
            RegexOptions.IgnoreCase
        );

        // Replace POWER(x,y) with ( x )^( y )
        expr = Regex.Replace(
            expr,
            @"POWER\s*\(\s*([^,]+)\s*,\s*([^)]+)\s*\)",
            @"($1)^($2)",
            RegexOptions.IgnoreCase
        );

        // Handle Max/Min using IIF
        expr = Regex.Replace(expr, @"Max\(([^,]+),([^)]+)\)", "IIF($1 > $2, $1, $2)");
        expr = Regex.Replace(expr, @"Min\(([^,]+),([^)]+)\)", "IIF($1 < $2, $1, $2)");

        Console.WriteLine($"Transformed expression: {expr}");

        return expr;
    }
}
