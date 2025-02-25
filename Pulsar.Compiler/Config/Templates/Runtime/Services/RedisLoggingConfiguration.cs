// File: Pulsar.Compiler/Config/Templates/Runtime/Services/RedisLoggingConfiguration.cs

using System;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace Beacon.Runtime.Services
{
    public static class RedisLoggingConfiguration
    {
        private const string DefaultLogPath = "logs/redis/redis-{Date}.log";

        public static ILogger ConfigureRedisLogger(
            RedisConfiguration config,
            string? logPath = null
        )
        {
            var loggerConfig = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithThreadId()
                .WriteTo.Console(
                    restrictedToMinimumLevel: LogEventLevel.Information,
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{ThreadId}] Redis: {Message:lj}{NewLine}{Exception}"
                );

            // Configure file logging
            var path = logPath ?? DefaultLogPath;
            loggerConfig.WriteTo.File(
                new CompactJsonFormatter(),
                path,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                restrictedToMinimumLevel: LogEventLevel.Debug
            );

            // Add structured event logging for metrics
            loggerConfig.WriteTo.File(
                new CompactJsonFormatter(),
                "logs/redis/metrics/metrics-{Date}.json",
                restrictedToMinimumLevel: LogEventLevel.Information
            );

            // Add separate error log
            loggerConfig.WriteTo.Logger(lc =>
                lc.Filter.ByIncludingOnly(e => e.Level >= LogEventLevel.Error)
                    .WriteTo.File(
                        "logs/redis/errors/error-{Date}.log",
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 30
                    )
            );

            return loggerConfig.CreateLogger();
        }

        public static void EnsureLogDirectories()
        {
            var directories = new[] { "logs/redis", "logs/redis/metrics", "logs/redis/errors" };

            foreach (var dir in directories)
            {
                if (!System.IO.Directory.Exists(dir))
                {
                    System.IO.Directory.CreateDirectory(dir);
                }
            }
        }
    }
}
