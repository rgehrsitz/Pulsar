// File: Tests/TestUtilities/LoggingConfig.cs
using Serilog;
using Serilog.Debugging;
using System.Diagnostics;

namespace Pulsar.Tests.TestUtilities
{
    public static class LoggingConfig
    {
        private static Serilog.ILogger? _logger;
        
        public static Serilog.ILogger GetLogger()
        {
            if (_logger == null)
            {
                _logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .WriteTo.Debug()          // Sends log events to the debug output (Debug.WriteLine)
                    .WriteTo.Console()        // Optionally write to the console as well
                    .CreateLogger();
                
                // Optional: Direct Serilog self-logging to the debug output
                SelfLog.Enable(message => Debug.WriteLine(message));
            }
            return _logger;
        }
    }
}
