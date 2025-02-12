// File: Pulsar.Compiler/Config/Templates/Runtime/Buffers/IDateTimeProvider.cs

namespace Pulsar.Runtime.Buffers
{
    public interface IDateTimeProvider
    {
        DateTime UtcNow { get; }
    }
}
