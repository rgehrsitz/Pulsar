using Microsoft.Extensions.Logging;
using Serilog.Core;

namespace Pulsar.Tests.TestUtilities
{
    public class LoggerAdapter(Microsoft.Extensions.Logging.ILogger msLogger) : Serilog.ILogger
    {
        private readonly Microsoft.Extensions.Logging.ILogger _msLogger = msLogger ?? throw new ArgumentNullException(nameof(msLogger));

        public Serilog.ILogger ForContext(ILogEventEnricher enricher)
        {
            return this;
        }

        public Serilog.ILogger ForContext(
            string propertyName,
            object value,
            bool destructureObjects = false
        )
        {
            return this;
        }

        public Serilog.ILogger ForContext<TSource>()
        {
            return this;
        }

        public Serilog.ILogger ForContext(Type source)
        {
            return this;
        }

        public void Write(Serilog.Events.LogEvent logEvent)
        {
            var level = logEvent.Level switch
            {
                Serilog.Events.LogEventLevel.Verbose => LogLevel.Trace,
                Serilog.Events.LogEventLevel.Debug => LogLevel.Debug,
                Serilog.Events.LogEventLevel.Information => LogLevel.Information,
                Serilog.Events.LogEventLevel.Warning => LogLevel.Warning,
                Serilog.Events.LogEventLevel.Error => LogLevel.Error,
                Serilog.Events.LogEventLevel.Fatal => LogLevel.Critical,
                _ => LogLevel.Information,
            };

            _msLogger.Log(level, logEvent.Exception, logEvent.RenderMessage());
        }

        public void Write(Serilog.Events.LogEventLevel level, string messageTemplate)
        {
            var msLevel = level switch
            {
                Serilog.Events.LogEventLevel.Verbose => LogLevel.Trace,
                Serilog.Events.LogEventLevel.Debug => LogLevel.Debug,
                Serilog.Events.LogEventLevel.Information => LogLevel.Information,
                Serilog.Events.LogEventLevel.Warning => LogLevel.Warning,
                Serilog.Events.LogEventLevel.Error => LogLevel.Error,
                Serilog.Events.LogEventLevel.Fatal => LogLevel.Critical,
                _ => LogLevel.Information,
            };

            _msLogger.Log(msLevel, messageTemplate);
        }

        public void Write<T>(
            Serilog.Events.LogEventLevel level,
            string messageTemplate,
            T propertyValue
        )
        {
            var msLevel = level switch
            {
                Serilog.Events.LogEventLevel.Verbose => LogLevel.Trace,
                Serilog.Events.LogEventLevel.Debug => LogLevel.Debug,
                Serilog.Events.LogEventLevel.Information => LogLevel.Information,
                Serilog.Events.LogEventLevel.Warning => LogLevel.Warning,
                Serilog.Events.LogEventLevel.Error => LogLevel.Error,
                Serilog.Events.LogEventLevel.Fatal => LogLevel.Critical,
                _ => LogLevel.Information,
            };

            _msLogger.Log(msLevel, messageTemplate, propertyValue);
        }

        public void Write<T0, T1>(
            Serilog.Events.LogEventLevel level,
            string messageTemplate,
            T0 propertyValue0,
            T1 propertyValue1
        )
        {
            var msLevel = level switch
            {
                Serilog.Events.LogEventLevel.Verbose => LogLevel.Trace,
                Serilog.Events.LogEventLevel.Debug => LogLevel.Debug,
                Serilog.Events.LogEventLevel.Information => LogLevel.Information,
                Serilog.Events.LogEventLevel.Warning => LogLevel.Warning,
                Serilog.Events.LogEventLevel.Error => LogLevel.Error,
                Serilog.Events.LogEventLevel.Fatal => LogLevel.Critical,
                _ => LogLevel.Information,
            };

            _msLogger.Log(msLevel, messageTemplate, propertyValue0, propertyValue1);
        }

        public void Write<T0, T1, T2>(
            Serilog.Events.LogEventLevel level,
            string messageTemplate,
            T0 propertyValue0,
            T1 propertyValue1,
            T2 propertyValue2
        )
        {
            var msLevel = level switch
            {
                Serilog.Events.LogEventLevel.Verbose => LogLevel.Trace,
                Serilog.Events.LogEventLevel.Debug => LogLevel.Debug,
                Serilog.Events.LogEventLevel.Information => LogLevel.Information,
                Serilog.Events.LogEventLevel.Warning => LogLevel.Warning,
                Serilog.Events.LogEventLevel.Error => LogLevel.Error,
                Serilog.Events.LogEventLevel.Fatal => LogLevel.Critical,
                _ => LogLevel.Information,
            };

            _msLogger.Log(msLevel, messageTemplate, propertyValue0, propertyValue1, propertyValue2);
        }

        public void Write(
            Serilog.Events.LogEventLevel level,
            Exception? exception,
            string messageTemplate
        )
        {
            var msLevel = level switch
            {
                Serilog.Events.LogEventLevel.Verbose => LogLevel.Trace,
                Serilog.Events.LogEventLevel.Debug => LogLevel.Debug,
                Serilog.Events.LogEventLevel.Information => LogLevel.Information,
                Serilog.Events.LogEventLevel.Warning => LogLevel.Warning,
                Serilog.Events.LogEventLevel.Error => LogLevel.Error,
                Serilog.Events.LogEventLevel.Fatal => LogLevel.Critical,
                _ => LogLevel.Information,
            };

            _msLogger.Log(msLevel, exception, messageTemplate);
        }

        public void Write<T>(
            Serilog.Events.LogEventLevel level,
            Exception? exception,
            string messageTemplate,
            T propertyValue
        )
        {
            var msLevel = level switch
            {
                Serilog.Events.LogEventLevel.Verbose => LogLevel.Trace,
                Serilog.Events.LogEventLevel.Debug => LogLevel.Debug,
                Serilog.Events.LogEventLevel.Information => LogLevel.Information,
                Serilog.Events.LogEventLevel.Warning => LogLevel.Warning,
                Serilog.Events.LogEventLevel.Error => LogLevel.Error,
                Serilog.Events.LogEventLevel.Fatal => LogLevel.Critical,
                _ => LogLevel.Information,
            };

            _msLogger.Log(msLevel, exception, messageTemplate, propertyValue);
        }

        public void Write<T0, T1>(
            Serilog.Events.LogEventLevel level,
            Exception? exception,
            string messageTemplate,
            T0 propertyValue0,
            T1 propertyValue1
        )
        {
            var msLevel = level switch
            {
                Serilog.Events.LogEventLevel.Verbose => LogLevel.Trace,
                Serilog.Events.LogEventLevel.Debug => LogLevel.Debug,
                Serilog.Events.LogEventLevel.Information => LogLevel.Information,
                Serilog.Events.LogEventLevel.Warning => LogLevel.Warning,
                Serilog.Events.LogEventLevel.Error => LogLevel.Error,
                Serilog.Events.LogEventLevel.Fatal => LogLevel.Critical,
                _ => LogLevel.Information,
            };

            _msLogger.Log(msLevel, exception, messageTemplate, propertyValue0, propertyValue1);
        }

        public void Write<T0, T1, T2>(
            Serilog.Events.LogEventLevel level,
            Exception? exception,
            string messageTemplate,
            T0 propertyValue0,
            T1 propertyValue1,
            T2 propertyValue2
        )
        {
            var msLevel = level switch
            {
                Serilog.Events.LogEventLevel.Verbose => LogLevel.Trace,
                Serilog.Events.LogEventLevel.Debug => LogLevel.Debug,
                Serilog.Events.LogEventLevel.Information => LogLevel.Information,
                Serilog.Events.LogEventLevel.Warning => LogLevel.Warning,
                Serilog.Events.LogEventLevel.Error => LogLevel.Error,
                Serilog.Events.LogEventLevel.Fatal => LogLevel.Critical,
                _ => LogLevel.Information,
            };

            _msLogger.Log(
                msLevel,
                exception,
                messageTemplate,
                propertyValue0,
                propertyValue1,
                propertyValue2
            );
        }

        public bool IsEnabled(Serilog.Events.LogEventLevel level)
        {
            var msLevel = level switch
            {
                Serilog.Events.LogEventLevel.Verbose => LogLevel.Trace,
                Serilog.Events.LogEventLevel.Debug => LogLevel.Debug,
                Serilog.Events.LogEventLevel.Information => LogLevel.Information,
                Serilog.Events.LogEventLevel.Warning => LogLevel.Warning,
                Serilog.Events.LogEventLevel.Error => LogLevel.Error,
                Serilog.Events.LogEventLevel.Fatal => LogLevel.Critical,
                _ => LogLevel.Information,
            };

            return _msLogger.IsEnabled(msLevel);
        }

        public void Verbose(string messageTemplate) => _msLogger.LogTrace(messageTemplate);

        public void Verbose<T>(string messageTemplate, T propertyValue) =>
            _msLogger.LogTrace(messageTemplate, propertyValue);

        public void Verbose<T0, T1>(string messageTemplate, T0 propertyValue0, T1 propertyValue1) =>
            _msLogger.LogTrace(messageTemplate, propertyValue0, propertyValue1);

        public void Verbose<T0, T1, T2>(
            string messageTemplate,
            T0 propertyValue0,
            T1 propertyValue1,
            T2 propertyValue2
        ) => _msLogger.LogTrace(messageTemplate, propertyValue0, propertyValue1, propertyValue2);

        public void Verbose(Exception? exception, string messageTemplate) =>
            _msLogger.LogTrace(exception, messageTemplate);

        public void Verbose<T>(Exception? exception, string messageTemplate, T propertyValue) =>
            _msLogger.LogTrace(exception, messageTemplate, propertyValue);

        public void Verbose<T0, T1>(
            Exception? exception,
            string messageTemplate,
            T0 propertyValue0,
            T1 propertyValue1
        ) => _msLogger.LogTrace(exception, messageTemplate, propertyValue0, propertyValue1);

        public void Verbose<T0, T1, T2>(
            Exception? exception,
            string messageTemplate,
            T0 propertyValue0,
            T1 propertyValue1,
            T2 propertyValue2
        ) =>
            _msLogger.LogTrace(
                exception,
                messageTemplate,
                propertyValue0,
                propertyValue1,
                propertyValue2
            );

        public void Debug(string messageTemplate) => _msLogger.LogDebug(messageTemplate);

        public void Debug<T>(string messageTemplate, T propertyValue) =>
            _msLogger.LogDebug(messageTemplate, propertyValue);

        public void Debug<T0, T1>(string messageTemplate, T0 propertyValue0, T1 propertyValue1) =>
            _msLogger.LogDebug(messageTemplate, propertyValue0, propertyValue1);

        public void Debug<T0, T1, T2>(
            string messageTemplate,
            T0 propertyValue0,
            T1 propertyValue1,
            T2 propertyValue2
        ) => _msLogger.LogDebug(messageTemplate, propertyValue0, propertyValue1, propertyValue2);

        public void Debug(Exception? exception, string messageTemplate) =>
            _msLogger.LogDebug(exception, messageTemplate);

        public void Debug<T>(Exception? exception, string messageTemplate, T propertyValue) =>
            _msLogger.LogDebug(exception, messageTemplate, propertyValue);

        public void Debug<T0, T1>(
            Exception? exception,
            string messageTemplate,
            T0 propertyValue0,
            T1 propertyValue1
        ) => _msLogger.LogDebug(exception, messageTemplate, propertyValue0, propertyValue1);

        public void Debug<T0, T1, T2>(
            Exception? exception,
            string messageTemplate,
            T0 propertyValue0,
            T1 propertyValue1,
            T2 propertyValue2
        ) =>
            _msLogger.LogDebug(
                exception,
                messageTemplate,
                propertyValue0,
                propertyValue1,
                propertyValue2
            );

        public void Information(string messageTemplate) =>
            _msLogger.LogInformation(messageTemplate);

        public void Information<T>(string messageTemplate, T propertyValue) =>
            _msLogger.LogInformation(messageTemplate, propertyValue);

        public void Information<T0, T1>(
            string messageTemplate,
            T0 propertyValue0,
            T1 propertyValue1
        ) => _msLogger.LogInformation(messageTemplate, propertyValue0, propertyValue1);

        public void Information<T0, T1, T2>(
            string messageTemplate,
            T0 propertyValue0,
            T1 propertyValue1,
            T2 propertyValue2
        ) =>
            _msLogger.LogInformation(
                messageTemplate,
                propertyValue0,
                propertyValue1,
                propertyValue2
            );

        public void Information(Exception? exception, string messageTemplate) =>
            _msLogger.LogInformation(exception, messageTemplate);

        public void Information<T>(Exception? exception, string messageTemplate, T propertyValue) =>
            _msLogger.LogInformation(exception, messageTemplate, propertyValue);

        public void Information<T0, T1>(
            Exception? exception,
            string messageTemplate,
            T0 propertyValue0,
            T1 propertyValue1
        ) => _msLogger.LogInformation(exception, messageTemplate, propertyValue0, propertyValue1);

        public void Information<T0, T1, T2>(
            Exception? exception,
            string messageTemplate,
            T0 propertyValue0,
            T1 propertyValue1,
            T2 propertyValue2
        ) =>
            _msLogger.LogInformation(
                exception,
                messageTemplate,
                propertyValue0,
                propertyValue1,
                propertyValue2
            );

        public void Warning(string messageTemplate) => _msLogger.LogWarning(messageTemplate);

        public void Warning<T>(string messageTemplate, T propertyValue) =>
            _msLogger.LogWarning(messageTemplate, propertyValue);

        public void Warning<T0, T1>(string messageTemplate, T0 propertyValue0, T1 propertyValue1) =>
            _msLogger.LogWarning(messageTemplate, propertyValue0, propertyValue1);

        public void Warning<T0, T1, T2>(
            string messageTemplate,
            T0 propertyValue0,
            T1 propertyValue1,
            T2 propertyValue2
        ) => _msLogger.LogWarning(messageTemplate, propertyValue0, propertyValue1, propertyValue2);

        public void Warning(Exception? exception, string messageTemplate) =>
            _msLogger.LogWarning(exception, messageTemplate);

        public void Warning<T>(Exception? exception, string messageTemplate, T propertyValue) =>
            _msLogger.LogWarning(exception, messageTemplate, propertyValue);

        public void Warning<T0, T1>(
            Exception? exception,
            string messageTemplate,
            T0 propertyValue0,
            T1 propertyValue1
        ) => _msLogger.LogWarning(exception, messageTemplate, propertyValue0, propertyValue1);

        public void Warning<T0, T1, T2>(
            Exception? exception,
            string messageTemplate,
            T0 propertyValue0,
            T1 propertyValue1,
            T2 propertyValue2
        ) =>
            _msLogger.LogWarning(
                exception,
                messageTemplate,
                propertyValue0,
                propertyValue1,
                propertyValue2
            );

        public void Error(string messageTemplate) => _msLogger.LogError(messageTemplate);

        public void Error<T>(string messageTemplate, T propertyValue) =>
            _msLogger.LogError(messageTemplate, propertyValue);

        public void Error<T0, T1>(string messageTemplate, T0 propertyValue0, T1 propertyValue1) =>
            _msLogger.LogError(messageTemplate, propertyValue0, propertyValue1);

        public void Error<T0, T1, T2>(
            string messageTemplate,
            T0 propertyValue0,
            T1 propertyValue1,
            T2 propertyValue2
        ) => _msLogger.LogError(messageTemplate, propertyValue0, propertyValue1, propertyValue2);

        public void Error(Exception? exception, string messageTemplate) =>
            _msLogger.LogError(exception, messageTemplate);

        public void Error<T>(Exception? exception, string messageTemplate, T propertyValue) =>
            _msLogger.LogError(exception, messageTemplate, propertyValue);

        public void Error<T0, T1>(
            Exception? exception,
            string messageTemplate,
            T0 propertyValue0,
            T1 propertyValue1
        ) => _msLogger.LogError(exception, messageTemplate, propertyValue0, propertyValue1);

        public void Error<T0, T1, T2>(
            Exception? exception,
            string messageTemplate,
            T0 propertyValue0,
            T1 propertyValue1,
            T2 propertyValue2
        ) =>
            _msLogger.LogError(
                exception,
                messageTemplate,
                propertyValue0,
                propertyValue1,
                propertyValue2
            );

        public void Fatal(string messageTemplate) => _msLogger.LogCritical(messageTemplate);

        public void Fatal<T>(string messageTemplate, T propertyValue) =>
            _msLogger.LogCritical(messageTemplate, propertyValue);

        public void Fatal<T0, T1>(string messageTemplate, T0 propertyValue0, T1 propertyValue1) =>
            _msLogger.LogCritical(messageTemplate, propertyValue0, propertyValue1);

        public void Fatal<T0, T1, T2>(
            string messageTemplate,
            T0 propertyValue0,
            T1 propertyValue1,
            T2 propertyValue2
        ) => _msLogger.LogCritical(messageTemplate, propertyValue0, propertyValue1, propertyValue2);

        public void Fatal(Exception? exception, string messageTemplate) =>
            _msLogger.LogCritical(exception, messageTemplate);

        public void Fatal<T>(Exception? exception, string messageTemplate, T propertyValue) =>
            _msLogger.LogCritical(exception, messageTemplate, propertyValue);

        public void Fatal<T0, T1>(
            Exception? exception,
            string messageTemplate,
            T0 propertyValue0,
            T1 propertyValue1
        ) => _msLogger.LogCritical(exception, messageTemplate, propertyValue0, propertyValue1);

        public void Fatal<T0, T1, T2>(
            Exception? exception,
            string messageTemplate,
            T0 propertyValue0,
            T1 propertyValue1,
            T2 propertyValue2
        ) =>
            _msLogger.LogCritical(
                exception,
                messageTemplate,
                propertyValue0,
                propertyValue1,
                propertyValue2
            );
    }
}
