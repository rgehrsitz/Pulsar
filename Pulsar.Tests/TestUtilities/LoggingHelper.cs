using Xunit.Abstractions;

namespace Pulsar.Tests.TestUtilities
{
    public static class LoggingHelper
    {
        // Helper method to create Serilog logger when Microsoft logger is provided
        public static Serilog.ILogger ToSerilogLogger(
            this Microsoft.Extensions.Logging.ILogger logger
        )
        {
            // Use our adapter to convert MS Logger to Serilog
            return new LoggerAdapter(logger);
        }

        // Helper method for tests that require Serilog.ILogger
        public static Serilog.ILogger GetSerilogLogger(ITestOutputHelper output)
        {
            return LoggingConfig.GetSerilogLoggerForTests(output);
        }
    }
}
