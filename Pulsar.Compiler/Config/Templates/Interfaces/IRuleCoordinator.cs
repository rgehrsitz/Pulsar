// File: Pulsar.Compiler/Config/Templates/Interfaces/IRuleCoordinator.cs
// Version: 1.0.0

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Beacon.Runtime.Interfaces
{
    public interface IRuleCoordinator
    {
        /// <summary>
        /// Gets the count of rules managed by this coordinator
        /// </summary>
        int RuleCount { get; }

        /// <summary>
        /// Gets the list of sensor names required by all rule groups
        /// </summary>
        string[] RequiredSensors { get; }

        /// <summary>
        /// Evaluates all rule groups with the given inputs and returns the combined outputs
        /// </summary>
        /// <param name="inputs">Dictionary of sensor values</param>
        /// <returns>Dictionary of output values</returns>
        Task<Dictionary<string, object>> ExecuteRulesAsync(Dictionary<string, object> inputs);
    }
}
