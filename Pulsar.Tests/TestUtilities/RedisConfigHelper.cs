// File: Pulsar.Tests/TestUtilities/RedisConfigHelper.cs

using DotNet.Testcontainers.Containers;

namespace Pulsar.Tests.TestUtilities
{
    public static class RedisConfigHelper
    {
        public static Dictionary<string, object> CreateRedisConfig(IContainer redisContainer)
        {
            if (redisContainer == null)
            {
                throw new ArgumentNullException(nameof(redisContainer));
            }

            return new Dictionary<string, object>
            {
                ["endpoints"] = new List<string>
                {
                    $"localhost:{redisContainer.GetMappedPublicPort(6379)}"
                },
                ["poolSize"] = 8,
                ["retryCount"] = 3,
                ["retryBaseDelayMs"] = 100,
                ["connectTimeout"] = 5000,
                ["syncTimeout"] = 1000,
                ["keepAlive"] = 60,
                ["password"] = string.Empty,
                ["ssl"] = false,
                ["allowAdmin"] = true
            };
        }
    }
}
