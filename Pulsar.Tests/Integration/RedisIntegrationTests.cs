using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Xunit;
using Xunit.Sdk;
using Pulsar.Runtime.Services;

namespace Pulsar.Tests.Integration
{
    public class RedisIntegrationTests : IDisposable
    {
        public required RedisService _singleNodeRedis;
        public required RedisService _clusterRedis;
        private readonly RedisConfiguration _singleNodeConfig;
        private readonly RedisConfiguration _clusterConfig;

        public RedisIntegrationTests()
        {
            _singleNodeConfig = new RedisConfiguration
            {
                Endpoints = new List<string> { "localhost:6379" },
                RetryCount = 3,
                RetryBaseDelayMs = 100,
                ConnectTimeoutMs = 2000,
                KeepAliveSeconds = 30
            };

            _clusterConfig = new RedisConfiguration
            {
                Endpoints = new List<string>
                {
                    "redis-node1:6379",
                    "redis-node2:6380",
                    "redis-node3:6381"
                },
                RetryCount = 3,
                RetryBaseDelayMs = 200,
                ConnectTimeoutMs = 3000,
                KeepAliveSeconds = 60
            };
        }

        [Fact]
        public async Task SingleNode_GetSetValues_Success()
        {
            // Skip if Redis is not available
            if (!IsRedisAvailable(_singleNodeConfig.Endpoints[0]))
            {
                return;
            }

            _singleNodeRedis = new RedisService(_singleNodeConfig);

            var testData = new Dictionary<string, double>
            {
                { "test:sensor1", 42.5 },
                { "test:sensor2", 23.1 }
            };

            await _singleNodeRedis.SetOutputValuesAsync(testData);

            var result = await _singleNodeRedis.GetSensorValuesAsync(testData.Keys);

            Assert.Equal(2, result.Count);
            Assert.Equal(42.5, result["test:sensor1"].Value);
            Assert.Equal(23.1, result["test:sensor2"].Value);
        }

        [Fact]
        public async Task Cluster_GetSetValues_Success()
        {
            // Skip if Redis cluster is not available
            if (!IsClusterAvailable(_clusterConfig.Endpoints))
            {
                return;
            }

            _clusterRedis = new RedisService(_clusterConfig);

            var testData = new Dictionary<string, double>
            {
                { "cluster:sensor1", 55.5 },
                { "cluster:sensor2", 66.6 }
            };

            await _clusterRedis.SetOutputValuesAsync(testData);

            var result = await _clusterRedis.GetSensorValuesAsync(testData.Keys);

            Assert.Equal(2, result.Count);
            Assert.Equal(55.5, result["cluster:sensor1"].Value);
            Assert.Equal(66.6, result["cluster:sensor2"].Value);
        }

        [Fact]
        public async Task ConnectionPool_HandlesMultipleOperations()
        {
            if (!IsRedisAvailable(_singleNodeConfig.Endpoints[0]))
            {
                return;
            }

            _singleNodeRedis = new RedisService(_singleNodeConfig);

            var tasks = new List<Task>();
            for (int i = 0; i < 100; i++)
            {
                var sensorData = new Dictionary<string, double>
                {
                    { $"concurrent:sensor{i}", i }
                };
                tasks.Add(_singleNodeRedis.SetOutputValuesAsync(sensorData));
            }

            await Task.WhenAll(tasks);

            var allKeys = new List<string>();
            for (int i = 0; i < 100; i++)
            {
                allKeys.Add($"concurrent:sensor{i}");
            }

            var results = await _singleNodeRedis.GetSensorValuesAsync(allKeys);
            Assert.Equal(100, results.Count);
        }

        private bool IsRedisAvailable(string endpoint)
        {
            try
            {
                var parts = endpoint.Split(':');
                using var client = new System.Net.Sockets.TcpClient();
                client.Connect(parts[0], int.Parse(parts[1]));
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool IsClusterAvailable(List<string> endpoints)
        {
            return endpoints.All(IsRedisAvailable);
        }

        public void Dispose()
        {
            _singleNodeRedis?.Dispose();
            _clusterRedis?.Dispose();
        }
    }
}
