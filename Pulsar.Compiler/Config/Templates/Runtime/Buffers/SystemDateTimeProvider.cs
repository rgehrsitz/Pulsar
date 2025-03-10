// File: Pulsar.Compiler/Config/Templates/Runtime/Buffers/SystemDateTimeProvider.cs

using System;

namespace Beacon.Runtime.Buffers
{
    public class SystemDateTimeProvider : IDateTimeProvider
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }
}