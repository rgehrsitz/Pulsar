// Generated code - do not modify directly
// Generated at: 2025-02-09 23:26:45 UTC

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Serilog;
using Prometheus;
using Pulsar.Runtime.Buffers;
using Pulsar.Runtime;

namespace Pulsar.Runtime.Rules
{
    public interface ICompiledRules
    {
        void Evaluate(Dictionary<string, double> inputs, Dictionary<string, double> outputs, RingBufferManager bufferManager);
    }
}
