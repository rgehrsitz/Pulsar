// File: Pulsar.Compiler/Config/Templates/Interfaces/IRuleCoordinator.cs

using System.Collections.Generic;

namespace Pulsar.Compiler.Config.Templates.Interfaces
{
    public interface IRuleCoordinator
    {
        void EvaluateRules(Dictionary<string, double> inputs, Dictionary<string, double> outputs);
        void ProcessInputs(Dictionary<string, string> inputs);
        Dictionary<string, string> GetOutputs();
    }
}
