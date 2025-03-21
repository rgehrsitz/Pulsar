using Microsoft.Extensions.Logging;

namespace Pulsar.Tests.Integration.Helpers
{
    /// <summary>
    /// Helper for setting up directory structures required for tests
    /// </summary>
    public static class DirectorySetup
    {
        /// <summary>
        /// Ensures that all required directories exist for end-to-end tests
        /// </summary>
        public static void EnsureTestDirectories(string basePath, ILogger logger)
        {
            try
            {
                // Make sure the main output directory exists
                if (!Directory.Exists(basePath))
                {
                    Directory.CreateDirectory(basePath);
                    logger.LogInformation("Created base output directory: {Path}", basePath);
                }

                // Create a permanent rules directory to store test rules
                var rulesDir = Path.Combine(Directory.GetCurrentDirectory(), "TestRules");
                if (!Directory.Exists(rulesDir))
                {
                    Directory.CreateDirectory(rulesDir);
                    logger.LogInformation("Created test rules directory: {Path}", rulesDir);
                }

                // Create a temporary directory for compilation artifacts
                var tempDir = Path.Combine(basePath, "temp");
                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir);
                    logger.LogInformation("Created temporary directory: {Path}", tempDir);
                }

                // Create directory for logs
                var logsDir = Path.Combine(basePath, "logs");
                if (!Directory.Exists(logsDir))
                {
                    Directory.CreateDirectory(logsDir);
                    logger.LogInformation("Created logs directory: {Path}", logsDir);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create required directories");
                throw;
            }
        }

        /// <summary>
        /// Cleans up temporary test files but preserves logs and important output
        /// </summary>
        public static void CleanupTempFiles(string basePath, ILogger logger)
        {
            try
            {
                var tempDir = Path.Combine(basePath, "temp");
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                    logger.LogInformation("Cleaned up temporary directory: {Path}", tempDir);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to clean up temporary files - this is not critical");
            }
        }
    }
}
