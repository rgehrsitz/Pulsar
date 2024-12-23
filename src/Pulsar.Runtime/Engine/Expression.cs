
using System;
using System.Data;

namespace Pulsar.Runtime.Engine;

public class Expression
{
    private readonly string _expression;

    public Expression(string expression)
    {
        _expression = expression;
    }

    public bool Evaluate(SimpleContext context)
    {
        var table = new DataTable();
        // Evaluate as a boolean
        var result = table.Compute(_expression, "");
        return Convert.ToBoolean(result);
    }
}