// File: Pulsar.Compiler/Config/Templates/Runtime/Buffers/SystemDateTimeProvider.cs

namespace Pulsar.Runtime.Buffers
{
    public class SystemDateTimeProvider : IDateTimeProvider
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }
}
