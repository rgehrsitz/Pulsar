// File: Pulsar.Compiler/Properties/AssemblyInfo.cs

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Pulsar.Tests")]
// Allow Serilog to access internal classes for logging
[assembly: InternalsVisibleTo("Serilog")]
[assembly: InternalsVisibleTo("Serilog.Sinks.Console")]
[assembly: InternalsVisibleTo("Serilog.Sinks.File")]
