using Microsoft.Extensions.Logging;
// Fix the namespace reference
using Pulsar.Tests.Mocks; // Use our mock classes instead

namespace Pulsar.Tests.Integration
{
    public static class RedisTestUtilities
    {
        /// <summary>
        /// Tests Redis service with cluster configuration
        /// </summary>
        public static async Task TestClusterConfiguration(
            RedisTestFixture fixture,
            string uniquePrefix,
            ILogger logger
        )
        {
            // This is a simulated cluster test since we can't easily create a real Redis cluster in tests
            // In a real environment, this would connect to multiple Redis nodes

            var config = new RedisConfiguration
            {
                Cluster = new ClusterConfig
                {
                    Endpoints = new[] { fixture.Redis.Configuration },
                    PoolSize = 4,
                    RetryCount = 3,
                    RetryBaseDelayMs = 100,
                    ConnectTimeout = 5000,
                    SyncTimeout = 1000,
                    KeepAlive = 60,
                },
            };

            // Create a service with cluster config
            var service = new RedisService(config, new LoggerFactory());

            // Test basic operations
            var key = $"{uniquePrefix}:cluster";
            var value = "cluster-test-value";

            await service.SetValue(key, value);
            var result = await service.GetValue(key);

            Assert.Equal(value, result);

            logger.LogInformation("Redis cluster configuration test passed");
        }

        /// <summary>
        /// Tests Redis service with high availability configuration
        /// </summary>
        public static async Task TestHighAvailabilityConfiguration(
            RedisTestFixture fixture,
            string uniquePrefix,
            ILogger logger
        )
        {
            // This is a simulated HA test since we can't easily create master/replica setup in tests
            // In a real environment, this would connect to a master and replica nodes

            var config = new RedisConfiguration
            {
                HighAvailability = new HighAvailabilityConfig
                {
                    Endpoints = new[] { fixture.Redis.Configuration },
                    PoolSize = 4,
                    ReplicaOnly = false,
                    RetryCount = 3,
                    RetryBaseDelayMs = 100,
                },
            };

            // Create a service with HA config
            var service = new RedisService(config, new LoggerFactory());

            // Test basic operations
            var key = $"{uniquePrefix}:ha";
            var value = "ha-test-value";

            await service.SetValue(key, value);
            var result = await service.GetValue(key);

            Assert.Equal(value, result);

            logger.LogInformation("Redis high availability configuration test passed");
        }

        /// <summary>
        /// Tests Redis service with simulated failover
        /// </summary>
        public static async Task TestFailoverHandling(string uniquePrefix, ILogger logger)
        {
            // Create a configuration with multiple endpoints - one real and one non-existent
            var config = new RedisConfiguration
            {
                SingleNode = new SingleNodeConfig
                {
                    Endpoints = new[] { "localhost:6379", "nonexistent:1234" },
                    PoolSize = 2,
                    RetryCount = 3,
                    RetryBaseDelayMs = 100,
                },
            };

            // Create a service with this config - it should connect to the working endpoint
            var service = new RedisService(config, new LoggerFactory());

            // Test basic operations
            var key = $"{uniquePrefix}:failover";
            var value = "failover-test-value";

            await service.SetValue(key, value);
            var result = await service.GetValue(key);

            Assert.Equal(value, result);

            logger.LogInformation("Redis failover handling test passed");
        }
    }
}
