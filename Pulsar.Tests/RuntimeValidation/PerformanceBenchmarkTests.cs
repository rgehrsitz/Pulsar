// File: Pulsar.Tests/RuntimeValidation/PerformanceBenchmarkTests.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Pulsar.Tests.RuntimeValidation
{
    public class PerformanceBenchmarkTests : IClassFixture<RuntimeValidationFixture>
    {
        private readonly RuntimeValidationFixture _fixture;
        private readonly ITestOutputHelper _output;

        public PerformanceBenchmarkTests(RuntimeValidationFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        [Fact(Skip = "Performance tests are currently disabled for this PR")]
        public async Task Benchmark_IncreasingRuleCount_MeasuresScalability()
        {
            // Create rule sets with different numbers of rules
            var ruleCounts = new[] { 1, 5, 10, 25, 50 };
            var results = new Dictionary<int, (double avgMs, double p95Ms)>();

            foreach (var ruleCount in ruleCounts)
            {
                _output.WriteLine($"Testing with {ruleCount} rules...");

                // Generate rules
                var ruleFile = GenerateRules(ruleCount);

                // Build project
                var success = await _fixture.BuildTestProject(new[] { ruleFile });
                Assert.True(success, $"Project with {ruleCount} rules should build successfully");

                // Run benchmark
                var (avgMs, p95Ms) = await RunBenchmark(ruleCount);
                results[ruleCount] = (avgMs, p95Ms);
            }

            // Output results
            _output.WriteLine("\nPerformance scaling with rule count:");
            _output.WriteLine("RuleCount\tAvg (ms)\tP95 (ms)");
            foreach (var (count, (avg, p95)) in results.OrderBy(r => r.Key))
            {
                _output.WriteLine($"{count}\t\t{avg:F2}\t\t{p95:F2}");
            }

            // Calculate scaling factor (how much slower per additional rule)
            if (ruleCounts.Length >= 2)
            {
                var minCount = ruleCounts.Min();
                var maxCount = ruleCounts.Max();
                var minTime = results[minCount].avgMs;
                var maxTime = results[maxCount].avgMs;

                var scalingFactor = (maxTime - minTime) / (maxCount - minCount);
                _output.WriteLine($"\nScaling factor: ~{scalingFactor:F2}ms per additional rule");

                // It should scale reasonably linearly (though not perfectly)
                Assert.True(scalingFactor < 10, "Scaling factor should be less than 10ms per rule");
            }
        }

        [Fact(Skip = "Performance tests are currently disabled for this PR")]
        public async Task Benchmark_IncreasingRuleComplexity_MeasuresPerformanceImpact()
        {
            // Create rules with different complexity levels
            var complexityLevels = new[] { 1, 3, 5, 10 };
            var results = new Dictionary<int, (double avgMs, double p95Ms)>();

            foreach (var complexity in complexityLevels)
            {
                _output.WriteLine($"Testing with complexity level {complexity}...");

                // Generate a rule with specified complexity
                var ruleFile = GenerateComplexRule(complexity);

                // Build project
                var success = await _fixture.BuildTestProject(new[] { ruleFile });
                Assert.True(
                    success,
                    $"Project with complexity {complexity} should build successfully"
                );

                // Run benchmark
                var (avgMs, p95Ms) = await RunBenchmark(complexity);
                results[complexity] = (avgMs, p95Ms);
            }

            // Output results
            _output.WriteLine("\nPerformance scaling with rule complexity:");
            _output.WriteLine("Complexity\tAvg (ms)\tP95 (ms)");
            foreach (var (complexity, (avg, p95)) in results.OrderBy(r => r.Key))
            {
                _output.WriteLine($"{complexity}\t\t{avg:F2}\t\t{p95:F2}");
            }
        }

        [Fact(Skip = "Performance tests are currently disabled for this PR")]
        public async Task Benchmark_ConcurrentRuleExecution_MeasuresThroughput()
        {
            // Generate a moderate set of rules
            var ruleFile = GenerateRules(20);

            // Build project
            var success = await _fixture.BuildTestProject(new[] { ruleFile });
            Assert.True(success, "Project should build successfully");

            // Prepare test inputs
            var inputs = new Dictionary<string, object>
            {
                { "input:a", 100 },
                { "input:b", 200 },
                { "input:c", 300 },
            };

            // Run concurrent executions
            var concurrencyLevels = new[] { 1, 2, 4, 8, 16 };
            var results = new Dictionary<int, (double throughput, double avgLatency)>();

            foreach (var concurrency in concurrencyLevels)
            {
                _output.WriteLine($"Testing with concurrency level {concurrency}...");

                var stopwatch = new Stopwatch();
                stopwatch.Start();

                // Run tasks in parallel
                var tasks = new List<Task<(bool success, Dictionary<string, object>? outputs)>>();
                for (int i = 0; i < concurrency; i++)
                {
                    tasks.Add(_fixture.ExecuteRules(inputs));
                }

                // Wait for all to complete
                await Task.WhenAll(tasks);

                stopwatch.Stop();
                var totalTime = stopwatch.Elapsed.TotalSeconds;
                var throughput = concurrency / totalTime;
                var avgLatency = totalTime * 1000 / concurrency;

                results[concurrency] = (throughput, avgLatency);

                // Verify all executions succeeded
                Assert.All(tasks, task => Assert.True(task.Result.success));
            }

            // Output results
            _output.WriteLine("\nPerformance scaling with concurrency:");
            _output.WriteLine("Concurrency\tThroughput (ops/sec)\tAvg Latency (ms)");
            foreach (var (concurrency, (throughput, latency)) in results.OrderBy(r => r.Key))
            {
                _output.WriteLine($"{concurrency}\t\t{throughput:F2}\t\t\t{latency:F2}");
            }
        }

        private string GenerateRules(int count)
        {
            var sb = new StringBuilder();
            sb.AppendLine("rules:");

            for (int i = 1; i <= count; i++)
            {
                sb.AppendLine(
                    $@"  - name: 'BenchmarkRule{i}'
    description: 'Generated benchmark rule {i}'
    conditions:
      all:
        - condition:
            type: comparison
            sensor: 'input:a'
            operator: '>'
            value: {i * 10}
    actions:
      - set_value:
          key: 'output:result{i}'
          value_expression: 'input:a + input:b * {i}'"
                );
            }

            var filePath = Path.Combine(_fixture.OutputPath, $"benchmark-{count}rules.yaml");
            File.WriteAllText(filePath, sb.ToString());
            return filePath;
        }

        private string GenerateComplexRule(int complexity)
        {
            var sb = new StringBuilder();
            sb.AppendLine("rules:");
            sb.AppendLine(
                $@"  - name: 'ComplexRule{complexity}'
    description: 'Rule with complexity level {complexity}'
    conditions:
      all:"
            );

            // Generate nested conditions
            for (int i = 1; i <= complexity; i++)
            {
                sb.AppendLine(
                    $@"        - condition:
            type: comparison
            sensor: 'input:a'
            operator: '>'
            value: {i * 10}"
                );
            }

            // Add a complex expression action
            sb.Append(
                $@"    actions:
      - set_value:
          key: 'output:complex_result'
          value_expression: '"
            );

            // Build a complex expression based on complexity level
            for (int i = 1; i <= complexity; i++)
            {
                if (i > 1)
                    sb.Append(" + ");
                sb.Append($"(input:a * {i} + input:b / {i})");
            }
            sb.AppendLine("'");

            var filePath = Path.Combine(_fixture.OutputPath, $"complex-{complexity}.yaml");
            File.WriteAllText(filePath, sb.ToString());
            return filePath;
        }

        private async Task<(double avgMs, double p95Ms)> RunBenchmark(int identifier)
        {
            // Prepare test inputs with randomization to avoid caching effects
            var random = new Random(42); // Fixed seed for reproducibility
            var inputs = new Dictionary<string, object>
            {
                { "input:a", 100 + random.Next(-10, 10) },
                { "input:b", 200 + random.Next(-20, 20) },
                { "input:c", 300 + random.Next(-30, 30) },
            };

            // Warmup
            for (int i = 0; i < 5; i++)
            {
                await _fixture.ExecuteRules(inputs);
            }

            // Measurement
            const int iterations = 20;
            var times = new List<double>();
            var stopwatch = new Stopwatch();

            for (int i = 0; i < iterations; i++)
            {
                // Slightly modify inputs for each run to avoid caching effects
                inputs["input:a"] = (int)inputs["input:a"] + random.Next(-5, 5);
                inputs["input:b"] = (int)inputs["input:b"] + random.Next(-5, 5);
                inputs["input:c"] = (int)inputs["input:c"] + random.Next(-5, 5);

                stopwatch.Restart();
                var (success, _) = await _fixture.ExecuteRules(inputs);
                stopwatch.Stop();

                Assert.True(success, $"Execution should succeed on iteration {i}");
                times.Add(stopwatch.Elapsed.TotalMilliseconds);
            }

            // Calculate statistics
            times.Sort();
            var avg = times.Average();
            var p95 = times[(int)(iterations * 0.95)];

            _output.WriteLine($"  ID: {identifier}, Avg: {avg:F2}ms, P95: {p95:F2}ms");
            return (avg, p95);
        }
    }
}
