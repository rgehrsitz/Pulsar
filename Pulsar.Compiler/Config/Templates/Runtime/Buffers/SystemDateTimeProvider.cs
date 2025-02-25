// File: Pulsar.Compiler/Config/Templates/Runtime/Buffers/SystemDateTimeProvider.cs

using System;

namespace Generated.Buffers
{
    public class SystemDateTimeProvider : IDateTimeProvider
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }
}
