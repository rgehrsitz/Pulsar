// File: Pulsar.Runtime/Interfaces/IRuleCoordinator.cs
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Beacon.Runtime.Interfaces
{
    public interface IRuleCoordinator
    {
        /// <summary>
        /// Gets the list of sensor names required by all rule groups
        /// </summary>
        string[] RequiredSensors { get; }

        /// <summary>
        /// Evaluates all rule groups with the given inputs and returns the combined outputs
        /// </summary>
        /// <param name="inputs">Dictionary of sensor values</param>
        /// <param name="outputs">Dictionary of output values</param>
        /// <returns></returns>
        Task EvaluateRulesAsync(
            Dictionary<string, object> inputs,
            Dictionary<string, object> outputs
        );
    }
}