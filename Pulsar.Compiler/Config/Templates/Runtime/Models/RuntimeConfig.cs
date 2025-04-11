// File: Pulsar.Compiler/Config/Templates/Runtime/Models/RuntimeConfig.cs
// Version: 1.0.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Beacon.Runtime.Models
{
    public class RuntimeConfig
    {
        [JsonInclude]
        public List<string> ValidSensors { get; set; } = new List<string>();

        [JsonInclude]
        public int CycleTime { get; set; } = 100;

        [JsonInclude]
        public bool TestMode { get; set; } = false;

        [JsonInclude]
        public int TestModeCycleTimeMs { get; set; } = 250; // Longer cycle time for testing

        /// <summary>
        /// Returns the appropriate cycle time based on whether test mode is enabled
        /// </summary>
        [JsonIgnore]
        public int EffectiveCycleTimeMs => TestMode ? TestModeCycleTimeMs : CycleTime;

        [JsonInclude]
        public int BufferCapacity { get; set; } = 100;

        [JsonInclude]
        public RedisConfiguration Redis { get; set; } = new RedisConfiguration();

        public static RuntimeConfig LoadFromEnvironment()
        {
            // Try to load from embedded config first if it exists
            try
            {
                var embeddedConfigType = Type.GetType("Beacon.Runtime.Generated.EmbeddedConfig");
                if (embeddedConfigType != null)
                {
                    var configJsonProperty = embeddedConfigType.GetProperty("ConfigJson");
                    if (configJsonProperty != null)
                    {
                        var configJson = (string)configJsonProperty.GetValue(null);
                        var embeddedConfig = JsonSerializer.Deserialize<RuntimeConfig>(configJson);
                        if (embeddedConfig != null)
                        {
                            return embeddedConfig;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Fall back to environment variables
            }

            // Create default config
            var config = new RuntimeConfig();

            // Load Redis configuration from environment variables
            var redisEndpoints = Environment.GetEnvironmentVariable("REDIS_ENDPOINTS");
            if (!string.IsNullOrEmpty(redisEndpoints))
            {
                config.Redis.Endpoints = new List<string>(redisEndpoints.Split(','));
            }
            else
            {
                config.Redis.Endpoints = new List<string> { "localhost:6379" };
            }

            var redisPassword = Environment.GetEnvironmentVariable("REDIS_PASSWORD");
            if (!string.IsNullOrEmpty(redisPassword))
            {
                config.Redis.Password = redisPassword;
            }

            if (
                int.TryParse(
                    Environment.GetEnvironmentVariable("REDIS_POOL_SIZE"),
                    out var poolSize
                )
            )
            {
                config.Redis.PoolSize = poolSize;
            }

            if (
                int.TryParse(
                    Environment.GetEnvironmentVariable("REDIS_RETRY_COUNT"),
                    out var retryCount
                )
            )
            {
                config.Redis.RetryCount = retryCount;
            }

            if (
                int.TryParse(
                    Environment.GetEnvironmentVariable("REDIS_RETRY_DELAY_MS"),
                    out var retryDelay
                )
            )
            {
                config.Redis.RetryBaseDelayMs = retryDelay;
            }

            if (
                int.TryParse(
                    Environment.GetEnvironmentVariable("REDIS_CONNECT_TIMEOUT"),
                    out var connectTimeout
                )
            )
            {
                config.Redis.ConnectTimeout = connectTimeout;
            }

            if (
                int.TryParse(
                    Environment.GetEnvironmentVariable("REDIS_SYNC_TIMEOUT"),
                    out var syncTimeout
                )
            )
            {
                config.Redis.SyncTimeout = syncTimeout;
            }

            if (
                int.TryParse(
                    Environment.GetEnvironmentVariable("REDIS_KEEPALIVE"),
                    out var keepAlive
                )
            )
            {
                config.Redis.KeepAlive = keepAlive;
            }

            if (bool.TryParse(Environment.GetEnvironmentVariable("REDIS_SSL"), out var ssl))
            {
                config.Redis.Ssl = ssl;
            }

            if (
                bool.TryParse(
                    Environment.GetEnvironmentVariable("REDIS_ALLOW_ADMIN"),
                    out var allowAdmin
                )
            )
            {
                config.Redis.AllowAdmin = allowAdmin;
            }

            // Load cycle time from environment variables
            if (
                int.TryParse(Environment.GetEnvironmentVariable("CYCLE_TIME_MS"), out var cycleTime)
            )
            {
                config.CycleTime = cycleTime;
            }
            
            // Load test mode settings from environment variables
            if (bool.TryParse(Environment.GetEnvironmentVariable("TEST_MODE"), out var testMode))
            {
                config.TestMode = testMode;
            }

            // Load test mode cycle time from environment variables
            if (
                int.TryParse(
                    Environment.GetEnvironmentVariable("TEST_MODE_CYCLE_TIME_MS"),
                    out var testModeCycleTime
                )
            )
            {
                config.TestModeCycleTimeMs = testModeCycleTime;
            }

            // Load buffer capacity from environment variables
            if (
                int.TryParse(
                    Environment.GetEnvironmentVariable("BUFFER_CAPACITY"),
                    out var bufferCapacity
                )
            )
            {
                config.BufferCapacity = bufferCapacity;
            }

            return config;
        }
    }
}
