// File: Pulsar.Compiler/Core/IRuleCompiler.cs


using Pulsar.Compiler.Models;

namespace Pulsar.Compiler.Core
{
    public interface IRuleCompiler
    {
        CompilationResult Compile(RuleDefinition[] rules, CompilerOptions options);
    }
}
