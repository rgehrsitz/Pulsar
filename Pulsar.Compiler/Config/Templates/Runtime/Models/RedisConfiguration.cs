// File: Pulsar.Compiler/Config/Templates/Runtime/Models/RedisConfiguration.cs
// Version: 1.0.0

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Beacon.Runtime.Services;
using StackExchange.Redis;
using YamlDotNet.Serialization;

namespace Beacon.Runtime.Models
{
    public class RedisConfiguration
    {
        [JsonPropertyName("endpoints")]
        [YamlMember(Alias = "endpoints")]
        public List<string> Endpoints { get; set; } = new();

        [JsonPropertyName("password")]
        [YamlMember(Alias = "password")]
        public string Password { get; set; } = string.Empty;

        [JsonPropertyName("connectionTimeout")]
        [YamlMember(Alias = "connectionTimeout")]
        public int ConnectionTimeout { get; set; } = 5000;

        [JsonPropertyName("operationTimeout")]
        [YamlMember(Alias = "operationTimeout")]
        public int OperationTimeout { get; set; } = 1000;

        [JsonPropertyName("retryCount")]
        [YamlMember(Alias = "retryCount")]
        public int RetryCount { get; set; } = 3;

        [JsonPropertyName("retryDelayMs")]
        [YamlMember(Alias = "retryDelayMs")]
        public int RetryDelayMs { get; set; } = 200;
        
        // Metrics instance for tracking Redis operations
        public RedisMetrics Metrics { get; } = new RedisMetrics();
        
        // Health check instance for monitoring Redis health
        public RedisHealthCheck HealthCheck { get; } = new RedisHealthCheck();
        
        /// <summary>
        /// Converts the configuration to Redis connection options
        /// </summary>
        /// <returns>ConfigurationOptions for Redis connection</returns>
        public ConfigurationOptions ToRedisOptions()
        {
            var options = new ConfigurationOptions
            {
                Password = Password,
                ConnectTimeout = ConnectionTimeout,
                SyncTimeout = OperationTimeout,
                AbortOnConnectFail = false,
                AllowAdmin = true
            };

            foreach (var endpoint in Endpoints)
            {
                options.EndPoints.Add(endpoint);
            }

            return options;
        }
    }
}
