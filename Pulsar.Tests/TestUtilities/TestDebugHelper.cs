using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Pulsar.Tests.TestUtilities
{
    /// <summary>
    /// Helper class for debugging Pulsar end-to-end tests
    /// </summary>
    public static class TestDebugHelper
    {
        /// <summary>
        /// Dumps Redis keys and values to help debug test issues
        /// </summary>
        public static async Task DumpRedisContentsAsync(ConnectionMultiplexer redis, ILogger logger)
        {
            try
            {
                var db = redis.GetDatabase();
                var server = redis.GetServer(redis.GetEndPoints()[0]);

                logger.LogInformation("=== REDIS CONTENTS ===");

                int keyCount = 0;
                foreach (var key in server.Keys())
                {
                    keyCount++;
                    string keyType = (await db.KeyTypeAsync(key)).ToString();

                    switch (keyType)
                    {
                        case "String":
                            var stringValue = await db.StringGetAsync(key);
                            logger.LogInformation(
                                "Key: {Key} (String) = {Value}",
                                key,
                                stringValue
                            );
                            break;
                        case "Hash":
                            var hashEntries = await db.HashGetAllAsync(key);
                            logger.LogInformation(
                                "Key: {Key} (Hash) = {Count} entries",
                                key,
                                hashEntries.Length
                            );
                            foreach (var entry in hashEntries)
                            {
                                logger.LogInformation(
                                    "  - {Field}: {Value}",
                                    entry.Name,
                                    entry.Value
                                );
                            }
                            break;
                        default:
                            logger.LogInformation("Key: {Key} ({Type})", key, keyType);
                            break;
                    }
                }

                if (keyCount == 0)
                {
                    logger.LogWarning("No keys found in Redis");
                }

                logger.LogInformation("=== END REDIS CONTENTS ===");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to dump Redis contents");
            }
        }

        /// <summary>
        /// Dumps directory contents recursively to help debug test issues
        /// </summary>
        public static void DumpDirectoryContents(
            string path,
            ILogger logger,
            int maxDepth = 3,
            int currentDepth = 0
        )
        {
            try
            {
                if (currentDepth == 0)
                {
                    logger.LogInformation("=== DIRECTORY CONTENTS: {Path} ===", path);
                }

                if (currentDepth > maxDepth)
                {
                    logger.LogInformation(
                        "{Indent}(Max depth reached)",
                        new string(' ', currentDepth * 2)
                    );
                    return;
                }

                if (!Directory.Exists(path))
                {
                    logger.LogWarning("Directory does not exist: {Path}", path);
                    return;
                }

                foreach (var directory in Directory.GetDirectories(path))
                {
                    logger.LogInformation(
                        "{Indent}[DIR] {Name}",
                        new string(' ', currentDepth * 2),
                        Path.GetFileName(directory)
                    );
                    DumpDirectoryContents(directory, logger, maxDepth, currentDepth + 1);
                }

                foreach (var file in Directory.GetFiles(path))
                {
                    var fileInfo = new FileInfo(file);
                    logger.LogInformation(
                        "{Indent}[FILE] {Name} ({Size} bytes)",
                        new string(' ', currentDepth * 2),
                        Path.GetFileName(file),
                        fileInfo.Length
                    );

                    // For small text files that might be useful for debugging, show their contents
                    if (
                        fileInfo.Length < 4096
                        && (
                            file.EndsWith(".yaml")
                            || file.EndsWith(".yml")
                            || file.EndsWith(".json")
                            || file.EndsWith(".cs")
                            || file.EndsWith(".csproj")
                        )
                    )
                    {
                        try
                        {
                            var content = File.ReadAllText(file);
                            logger.LogInformation(
                                "{Indent}  Content: \n{Content}",
                                new string(' ', currentDepth * 2),
                                content
                            );
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(
                                "{Indent}  Could not read file: {Error}",
                                new string(' ', currentDepth * 2),
                                ex.Message
                            );
                        }
                    }
                }

                if (currentDepth == 0)
                {
                    logger.LogInformation("=== END DIRECTORY CONTENTS ===");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to dump directory contents for {Path}", path);
            }
        }
    }
}
