// File: Pulsar.Compiler/Config/Templates/Interfaces/IRuleCoordinator.cs

namespace Pulsar.Runtime.Rules
{
    public interface IRuleCoordinator
    {
        void EvaluateRules(Dictionary<string, double> inputs, Dictionary<string, double> outputs);
    }
}
