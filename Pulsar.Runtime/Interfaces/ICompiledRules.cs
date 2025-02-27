// File: Pulsar.Runtime/Interfaces/ICompiledRules.cs
using System.Collections.Generic;

namespace Beacon.Runtime.Interfaces
{
    /// <summary>
    /// Interface for compiled rule implementations
    /// </summary>
    public interface ICompiledRules
    {
        /// <summary>
        /// Gets the list of required input sensors for this rule
        /// </summary>
        string[] RequiredSensors { get; }

        /// <summary>
        /// Evaluates the rule with the provided inputs and adds results to the outputs dictionary
        /// </summary>
        /// <param name="inputs">Dictionary of input values</param>
        /// <param name="outputs">Dictionary of output values</param>
        void EvaluateRule(Dictionary<string, object> inputs, Dictionary<string, object> outputs);
    }
}