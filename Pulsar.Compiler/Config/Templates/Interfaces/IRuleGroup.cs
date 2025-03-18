// File: Pulsar.Compiler/Config/Templates/Interfaces/IRuleGroup.cs
// Version: 1.0.0

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Beacon.Runtime.Interfaces
{
    /// <summary>
    /// Represents a group of rules that can be evaluated together
    /// </summary>
    public interface IRuleGroup
    {
        /// <summary>
        /// Gets the name of this rule group
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the list of sensor names required by this rule group
        /// </summary>
        string[] RequiredSensors { get; }

        /// <summary>
        /// Evaluates the rules in this group with the given inputs and returns the outputs
        /// </summary>
        /// <param name="inputs">Dictionary of sensor values</param>
        /// <param name="outputs">Dictionary of output values</param>
        Task EvaluateRulesAsync(
            Dictionary<string, object> inputs,
            Dictionary<string, object> outputs
        );
    }
}
