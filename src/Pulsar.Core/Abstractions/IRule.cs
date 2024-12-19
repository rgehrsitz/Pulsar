namespace Pulsar.Core.Abstractions;

public interface IRule 
{
    string Name { get; }
    bool Evaluate();
}
