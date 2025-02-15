// File: Pulsar.Compiler/Exceptions/ValidationException.cs

using System;
using Serilog;

namespace Pulsar.Compiler.Exceptions
{
    public class ValidationException : Exception
    {
        private static readonly ILogger _logger = LoggingConfig.GetLogger();

        public ValidationException(string message) : base(message)
        {
            _logger.Error("Validation Error: {Message}", message);
        }

        public ValidationException(string message, Exception innerException) : base(message, innerException)
        {
            _logger.Error(innerException, "Validation Error: {Message}", message);
        }
    }
}
