// File: Pulsar.Tests/TestUtilities/LoggingConfig.cs
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;
using Xunit.Abstractions;
using Serilog.Sinks.XUnit;

namespace Pulsar.Tests.TestUtilities
{
    public static class LoggingConfig
    {
        // Add a method to get Serilog.ILogger for components that need it
        public static Serilog.ILogger GetSerilogLogger()
        {
            return new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .CreateLogger();
        }
        
        public static Serilog.ILogger GetSerilogLoggerForTests(ITestOutputHelper output)
        {
            return new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.TestOutput(output)
                .CreateLogger();
        }
        
        // Keep original methods returning Microsoft.Extensions.Logging.ILogger
        public static Microsoft.Extensions.Logging.ILogger GetLogger()
        {
            var serilogLogger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .CreateLogger();
                
            return new SerilogLoggerFactory(serilogLogger).CreateLogger("TestLogger");
        }
        
        public static Microsoft.Extensions.Logging.ILogger GetLoggerForTests(ITestOutputHelper output)
        {
            // Fix: Use the TestOutput sink which is available in the Serilog.Sinks.XUnit package
            var serilogLogger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.TestOutput(output)
                .CreateLogger();
                
            return new SerilogLoggerFactory(serilogLogger).CreateLogger("TestLogger");
        }
        
        // Adapter to convert Microsoft.Extensions.Logging.ILogger to Serilog.ILogger
        public static Serilog.ILogger ToSerilogLogger(Microsoft.Extensions.Logging.ILogger msLogger)
        {
            return new LoggerAdapter(msLogger);
        }
    }
}