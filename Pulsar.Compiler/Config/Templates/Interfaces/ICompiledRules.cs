// File: Pulsar.Compiler/Config/Templates/Interfaces/ICompiledRules.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Serilog;
using Pulsar.Runtime.Buffers;
using Pulsar.Runtime;

namespace Pulsar.Runtime.Rules
{
    public interface ICompiledRules
    {
        void Evaluate(Dictionary<string, double> inputs, Dictionary<string, double> outputs, RingBufferManager bufferManager);
    }
}
