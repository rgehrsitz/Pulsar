// File: Pulsar.Compiler/LoggingConfig.cs

using System;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace Pulsar.Compiler
{
    public static class LoggingConfig
    {
        private static Logger? _logger;
        private static readonly object _lock = new();

        public static Logger GetLogger()
        {
            if (_logger == null)
            {
                lock (_lock)
                {
                    if (_logger == null)
                    {
                        var config = new LoggerConfiguration()
                            .MinimumLevel.Debug()
                            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                            .MinimumLevel.Override("System", LogEventLevel.Information)
                            .Enrich.FromLogContext()
                            .Enrich.WithThreadId()
                            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{ThreadId}] {Message:lj}{NewLine}{Exception}")
                            .WriteTo.File(new CompactJsonFormatter(), "logs/pulsar-.log", 
                                rollingInterval: RollingInterval.Day,
                                retainedFileCountLimit: 30,
                                fileSizeLimitBytes: 100 * 1024 * 1024) // 100MB per file
                            .CreateLogger();

                        _logger = config;
                    }
                }
            }
            return _logger;
        }

        public static void CloseAndFlush()
        {
            _logger?.Dispose();
            _logger = null;
        }
    }
}
