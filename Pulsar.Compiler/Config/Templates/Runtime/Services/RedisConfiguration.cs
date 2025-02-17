// File: Pulsar.Compiler/Config/Templates/Runtime/Services/RedisConfiguration.cs

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using StackExchange.Redis;

namespace Pulsar.Runtime.Services
{
    public class RedisConfiguration
    {
        [JsonPropertyName("endpoints")]
        public List<string> Endpoints { get; set; } = new() { "localhost:6379" };

        [JsonPropertyName("poolSize")]
        public int PoolSize { get; set; } = Environment.ProcessorCount * 2;

        [JsonPropertyName("retryCount")]
        public int RetryCount { get; set; } = 3;

        [JsonPropertyName("retryBaseDelayMs")]
        public int RetryBaseDelayMs { get; set; } = 100;

        [JsonPropertyName("connectTimeout")]
        public int ConnectTimeoutMs { get; set; } = 5000;

        [JsonPropertyName("syncTimeout")]
        public int SyncTimeoutMs { get; set; } = 1000;

        [JsonPropertyName("keepAlive")]
        public int KeepAliveSeconds { get; set; } = 60;

        [JsonPropertyName("password")]
        public string? Password { get; set; }

        [JsonPropertyName("ssl")]
        public bool UseSsl { get; set; }

        [JsonPropertyName("allowAdmin")]
        public bool AllowAdmin { get; set; }

        [JsonPropertyName("healthCheck")]
        public RedisHealthCheckConfig HealthCheck { get; set; } = new();

        [JsonPropertyName("metrics")]
        public RedisMetricsConfig Metrics { get; set; } = new();

        public ConfigurationOptions ToRedisOptions()
        {
            var options = new ConfigurationOptions
            {
                AbortOnConnectFail = false,
                ConnectTimeout = ConnectTimeoutMs,
                SyncTimeout = SyncTimeoutMs,
                KeepAlive = KeepAliveSeconds,
                Password = Password,
                Ssl = UseSsl,
                AllowAdmin = AllowAdmin,
                ReconnectRetryPolicy = new ExponentialRetry(RetryBaseDelayMs)
            };

            foreach (var endpoint in Endpoints)
            {
                var parts = endpoint.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[1], out int port))
                {
                    options.EndPoints.Add(parts[0], port);
                }
            }

            if (Endpoints.Count > 1)
            {
                options.ServiceName = "PulsarRedisCluster";
            }

            options.CommandMap = CommandMap.Create(new HashSet<string>
            {
                "SUBSCRIBE",
                "UNSUBSCRIBE",
                "PUBLISH"
            }, false);

            return options;
        }

        public class RedisHealthCheckConfig
        {
            [JsonPropertyName("enabled")]
            public bool Enabled { get; set; } = true;

            [JsonPropertyName("intervalSeconds")]
            public int HealthCheckIntervalSeconds { get; set; } = 30;

            [JsonPropertyName("failureThreshold")]
            public int FailureThreshold { get; set; } = 5;

            [JsonPropertyName("timeoutMs")]
            public int TimeoutMs { get; set; } = 2000;
        }

        public class RedisMetricsConfig
        {
            [JsonPropertyName("enabled")]
            public bool Enabled { get; set; } = true;

            [JsonPropertyName("instanceName")]
            public string InstanceName { get; set; } = "default";

            [JsonPropertyName("samplingIntervalSeconds")]
            public int SamplingIntervalSeconds { get; set; } = 60;
        }
    }
}