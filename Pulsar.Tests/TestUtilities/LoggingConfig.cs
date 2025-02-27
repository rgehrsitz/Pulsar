using Serilog;

namespace Pulsar.Tests.TestUtilities
{
    public static class LoggingConfig
    {
        public static ILogger GetLogger()
        {
            return new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .CreateLogger();
        }
    }
}
