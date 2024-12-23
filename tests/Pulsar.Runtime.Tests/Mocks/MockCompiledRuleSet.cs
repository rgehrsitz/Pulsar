using System.Collections.Generic;
using Pulsar.Compiler.Models;

namespace Pulsar.Runtime.Tests.Mocks;

public class MockCompiledRuleSet : CompiledRuleSet
{
    public MockCompiledRuleSet() : base(
        new List<CompiledRule>(),
        0,
        new HashSet<string>(),
        new HashSet<string>())
    {
    }
}
