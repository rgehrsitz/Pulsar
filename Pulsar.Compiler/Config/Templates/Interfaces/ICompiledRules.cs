// File: Pulsar.Compiler/Config/Templates/Interfaces/ICompiledRules.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Beacon.Runtime;
using Beacon.Runtime.Buffers;
using Serilog;

namespace Beacon.Runtime.Rules
{
    public interface ICompiledRules
    {
        void Evaluate(
            Dictionary<string, double> inputs,
            Dictionary<string, double> outputs,
            RingBufferManager bufferManager
        );
    }
}
