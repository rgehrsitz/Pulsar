// File: Pulsar.Compiler/Config/Templates/Runtime/Buffers/IDateTimeProvider.cs

using System;

namespace Beacon.Runtime.Buffers
{
    public interface IDateTimeProvider
    {
        DateTime UtcNow { get; }
    }
}
