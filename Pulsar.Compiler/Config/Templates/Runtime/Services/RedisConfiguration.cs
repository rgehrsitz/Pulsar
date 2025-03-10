// File: Pulsar.Compiler/Config/Templates/Runtime/Services/RedisConfiguration.cs

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using StackExchange.Redis;
using YamlDotNet.Serialization;

namespace Beacon.Runtime.Services
{
    public class RedisConfiguration
    {
        [JsonPropertyName("endpoints")]
        [YamlMember(Alias = "endpoints")]
        public List<string> Endpoints { get; set; } = new() { "localhost:6379" };

        [JsonPropertyName("poolSize")]
        [YamlMember(Alias = "poolSize")]
        public int PoolSize { get; set; } = Environment.ProcessorCount * 2;

        [JsonPropertyName("retryCount")]
        [YamlMember(Alias = "retryCount")]
        public int RetryCount { get; set; } = 3;

        [JsonPropertyName("retryBaseDelayMs")]
        [YamlMember(Alias = "retryBaseDelayMs")]
        public int RetryBaseDelayMs { get; set; } = 100;

        [JsonPropertyName("connectTimeout")]
        [YamlMember(Alias = "connectTimeout")]
        public int ConnectTimeoutMs { get; set; } = 5000;

        [JsonPropertyName("syncTimeout")]
        [YamlMember(Alias = "syncTimeout")]
        public int SyncTimeoutMs { get; set; } = 1000;

        [JsonPropertyName("keepAlive")]
        [YamlMember(Alias = "keepAlive")]
        public int KeepAliveSeconds { get; set; } = 60;

        [JsonPropertyName("password")]
        [YamlMember(Alias = "password")]
        public string? Password { get; set; }

        [JsonPropertyName("ssl")]
        [YamlMember(Alias = "ssl")]
        public bool UseSsl { get; set; }

        [JsonPropertyName("allowAdmin")]
        [YamlMember(Alias = "allowAdmin")]
        public bool AllowAdmin { get; set; }

        [JsonPropertyName("healthCheck")]
        [YamlMember(Alias = "healthCheck")]
        public RedisHealthCheckConfig HealthCheck { get; set; } = new();

        [JsonPropertyName("metrics")]
        [YamlMember(Alias = "metrics")]
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
                ReconnectRetryPolicy = new ExponentialRetry(RetryBaseDelayMs),
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

            options.CommandMap = CommandMap.Create(
                new HashSet<string> { "SUBSCRIBE", "UNSUBSCRIBE", "PUBLISH" },
                false
            );

            return options;
        }

        public class RedisHealthCheckConfig
        {
            [JsonPropertyName("enabled")]
            [YamlMember(Alias = "enabled")]
            public bool Enabled { get; set; } = true;

            [JsonPropertyName("intervalSeconds")]
            [YamlMember(Alias = "intervalSeconds")]
            public int HealthCheckIntervalSeconds { get; set; } = 30;

            [JsonPropertyName("failureThreshold")]
            [YamlMember(Alias = "failureThreshold")]
            public int FailureThreshold { get; set; } = 5;

            [JsonPropertyName("timeoutMs")]
            [YamlMember(Alias = "timeoutMs")]
            public int TimeoutMs { get; set; } = 2000;
        }

        public class RedisMetricsConfig
        {
            [JsonPropertyName("enabled")]
            [YamlMember(Alias = "enabled")]
            public bool Enabled { get; set; } = true;

            [JsonPropertyName("instanceName")]
            [YamlMember(Alias = "instanceName")]
            public string InstanceName { get; set; } = "default";

            [JsonPropertyName("samplingIntervalSeconds")]
            [YamlMember(Alias = "samplingIntervalSeconds")]
            public int SamplingIntervalSeconds { get; set; } = 60;
        }
    }
}
