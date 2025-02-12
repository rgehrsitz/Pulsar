// File: Pulsar.Runtime/Rules/DefaultRuleCoordinator.cs

using System;
using System.Collections.Generic;
using Pulsar.Runtime.Buffers;
using Serilog;
using Pulsar.Compiler.Config.Templates.Interfaces;

namespace Pulsar.Runtime.Rules
{
    internal class DefaultRuleCoordinator : IRuleCoordinator
    {
        private readonly ILogger _logger;
        private readonly RingBufferManager _bufferManager;
        private bool _warnedNoRules;

        public DefaultRuleCoordinator(ILogger logger, RingBufferManager bufferManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _bufferManager =
                bufferManager ?? throw new ArgumentNullException(nameof(bufferManager));
        }

        public void EvaluateRules(
            Dictionary<string, double> inputs,
            Dictionary<string, double> outputs
        )
        {
            // Log warning only once to avoid flooding logs
            if (!_warnedNoRules)
            {
                _logger.Warning(
                    "No rules loaded - using default coordinator. This message will only appear once."
                );
                _warnedNoRules = true;
            }

            // Keep inputs flowing through to outputs for debugging
            foreach (var kvp in inputs)
            {
                outputs[kvp.Key] = kvp.Value;
            }

            // Update buffers even with no rules to maintain history
            _bufferManager.UpdateBuffers(inputs);
        }
    }
}
