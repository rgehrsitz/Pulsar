// File: Pulsar.Compiler/Config/Templates/Runtime/Configuration/ConfigurationService.cs
// Version: 1.0.0

using System;
using Beacon.Runtime.Models;
using Serilog;

namespace Beacon.Runtime.Configuration
{
    /// <summary>
    /// Service for accessing application configuration
    /// </summary>
    public static class ConfigurationService
    {
        private static RuntimeConfig? _runtimeConfig;
        private static readonly object _lockObj = new object();

        /// <summary>
        /// Gets the current RuntimeConfig instance, creating it if necessary
        /// </summary>
        public static RuntimeConfig GetRuntimeConfig()
        {
            if (_runtimeConfig == null)
            {
                lock (_lockObj)
                {
                    if (_runtimeConfig == null)
                    {
                        _runtimeConfig = RuntimeConfig.LoadFromEnvironment();
                    }
                }
            }
            return _runtimeConfig;
        }

        /// <summary>
        /// Updates the RuntimeConfig with the provided update action
        /// </summary>
        /// <param name="updateAction">Action to update the config</param>
        public static void UpdateConfig(Action<RuntimeConfig> updateAction)
        {
            lock (_lockObj)
            {
                var config = GetRuntimeConfig();
                updateAction(config);
                
                // Log changes for debugging
                Log.Information("Configuration updated. TestMode: {TestMode}, CycleTime: {CycleTime}, TestModeCycleTimeMs: {TestModeCycleTimeMs}",
                    config.TestMode, config.CycleTime, config.TestModeCycleTimeMs);
            }
        }

        /// <summary>
        /// Sets test mode and optionally overrides the test cycle time
        /// </summary>
        public static void SetTestMode(bool enabled, int? cycleTimeMs = null)
        {
            UpdateConfig(config => 
            {
                config.TestMode = enabled;
                if (cycleTimeMs.HasValue)
                {
                    config.TestModeCycleTimeMs = cycleTimeMs.Value;
                }
            });
        }
    }
}
