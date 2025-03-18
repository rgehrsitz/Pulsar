// File: Pulsar.Compiler/Config/Templates/Runtime/Rules/RuleBase.cs
// Version: 1.0.0

using System;
using System.Collections.Generic;
using Beacon.Runtime.Buffers;
using Microsoft.Extensions.Logging;

namespace Beacon.Runtime.Rules
{
    /// <summary>
    /// Base class for all rule implementations
    /// </summary>
    public abstract class RuleBase
    {
        protected readonly ILogger _logger;
        protected readonly RingBufferManager _bufferManager;

        protected RuleBase(ILogger logger, RingBufferManager bufferManager)
        {
            _logger = logger;
            _bufferManager = bufferManager;
        }

        /// <summary>
        /// Evaluates the rule with the given inputs and returns the outputs
        /// </summary>
        /// <param name="inputs">Dictionary of sensor values</param>
        /// <param name="outputs">Dictionary of output values</param>
        public abstract void Evaluate(
            Dictionary<string, double> inputs,
            Dictionary<string, double> outputs
        );
    }
}
