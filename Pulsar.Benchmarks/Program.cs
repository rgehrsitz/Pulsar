using BenchmarkDotNet.Running;
using Pulsar.Benchmarks;

var summary = BenchmarkRunner.Run<RuleEvaluationBenchmarks>();
Console.WriteLine("Benchmarks complete");
