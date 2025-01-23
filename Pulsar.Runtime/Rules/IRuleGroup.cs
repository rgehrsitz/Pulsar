// File: Pulsar.Runtime/Rules/IRuleGroup.cs

using System.Collections.Generic;
using Pulsar.Runtime.Buffers;

namespace Pulsar.Runtime.Rules
{
    /// <summary>
    /// Represents a group of rules that can be evaluated together
    /// </summary>
    public interface IRuleGroup
    {
        /// <summary>
        /// Evaluates all rules within this group
        /// </summary>
        /// <param name="inputs">Input sensor values</param>
        /// <param name="outputs">Output sensor values to be modified</param>
        /// <param name="bufferManager">Ring buffer manager for temporal conditions</param>
        void EvaluateGroup(
            Dictionary<string, double> inputs,
            Dictionary<string, double> outputs,
            RingBufferManager bufferManager
        );
    }
}