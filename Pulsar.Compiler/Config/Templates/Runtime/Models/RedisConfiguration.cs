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

        [JsonPropertyName("poolSize")]
        [YamlMember(Alias = "poolSize")]
        public int PoolSize { get; set; } = 8;

        [JsonPropertyName("connectTimeout")]
        [YamlMember(Alias = "connectTimeout")]
        public int ConnectTimeout { get; set; } = 5000;

        [JsonPropertyName("syncTimeout")]
        [YamlMember(Alias = "syncTimeout")]
        public int SyncTimeout { get; set; } = 1000;

        [JsonPropertyName("keepAlive")]
        [YamlMember(Alias = "keepAlive")]
        public int KeepAlive { get; set; } = 60;

        [JsonPropertyName("retryCount")]
        [YamlMember(Alias = "retryCount")]
        public int RetryCount { get; set; } = 3;

        [JsonPropertyName("retryBaseDelayMs")]
        [YamlMember(Alias = "retryBaseDelayMs")]
        public int RetryBaseDelayMs { get; set; } = 100;

        [JsonPropertyName("ssl")]
        [YamlMember(Alias = "ssl")]
        public bool Ssl { get; set; } = false;

        [JsonPropertyName("allowAdmin")]
        [YamlMember(Alias = "allowAdmin")]
        public bool AllowAdmin { get; set; } = false;
        
        // Metrics instance for tracking Redis operations
        public RedisMetrics Metrics { get; } = new RedisMetrics();
        
        // Health check instance for monitoring Redis health
        private RedisHealthCheck? _healthCheck;
        public RedisHealthCheck HealthCheck 
        { 
            get 
            {
                if (_healthCheck == null)
                {
                    _healthCheck = new RedisHealthCheck(this);
                }
                return _healthCheck;
            } 
        }
        
        /// <summary>
        /// Converts the configuration to Redis connection options
        /// </summary>
        /// <returns>ConfigurationOptions for Redis connection</returns>
        public ConfigurationOptions ToRedisOptions()
        {
            var options = new ConfigurationOptions
            {
                Password = Password,
                ConnectTimeout = ConnectTimeout,
                SyncTimeout = SyncTimeout,
                KeepAlive = KeepAlive,
                AbortOnConnectFail = false,
                AllowAdmin = AllowAdmin,
                Ssl = Ssl
            };

            foreach (var endpoint in Endpoints)
            {
                options.EndPoints.Add(endpoint);
            }

            return options;
        }
    }
}
