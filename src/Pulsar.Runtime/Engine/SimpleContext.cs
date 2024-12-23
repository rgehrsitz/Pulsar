
using System.Collections.Generic;

namespace Pulsar.Runtime.Engine;

public class SimpleContext
{
    private readonly Dictionary<string, double> _values = new();

    public void SetValue(string name, double value)
    {
        _values[name] = value;
    }

    public double GetValue(string name)
    {
        return _values[name];
    }
}