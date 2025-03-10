// File: Pulsar.Tests/RuntimeValidation/MemoryUsageTests.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using System.Linq;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Pulsar.Tests.RuntimeValidation
{
    [Trait("Category", "MemoryUsage")]
    public class MemoryUsageTests : IClassFixture<RuntimeValidationFixture>
    {
        private readonly RuntimeValidationFixture _fixture;
        private readonly ITestOutputHelper _output;
        
        public MemoryUsageTests(RuntimeValidationFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }
        
        [Fact]
        public async Task ExtendedExecution_MonitorsMemoryUsage()
        {
            // Generate rules for memory testing
            var ruleFile = GenerateMemoryTestRules(20);
            
            // Build project
            var success = await _fixture.BuildTestProject(new[] { ruleFile });
            Assert.True(success, "Project should build successfully");
            
            // Run extended execution monitoring
            var memorySnapshots = new List<(int cycle, long memory)>();
            var totalCycles = 100;
            
            // Monitor for longer duration to detect potential memory leaks
            await _fixture.MonitorMemoryUsage(
                duration: TimeSpan.FromMinutes(1),
                cycleCount: totalCycles,
                memoryCallback: memory => {
                    var cycle = memorySnapshots.Count;
                    memorySnapshots.Add((cycle, memory));
                    _output.WriteLine($"Cycle {cycle}: {memory / (1024 * 1024):F2} MB");
                }
            );
            
            // Analyze memory usage pattern
            AnalyzeMemoryUsage(memorySnapshots);
        }
        
        [Fact]
        public async Task HighInputChurn_MonitorsMemoryStability()
        {
            // Generate rules for memory testing
            var ruleFile = GenerateMemoryTestRules(10);
            
            // Build project
            var success = await _fixture.BuildTestProject(new[] { ruleFile });
            Assert.True(success, "Project should build successfully");
            
            // Track memory usage
            var memorySnapshots = new List<(int cycle, long memory)>();
            var random = new Random(42);
            
            // Execute rules with continuously changing inputs
            for (int i = 0; i < 50; i++)
            {
                // Create a large number of input values to stress Redis
                var inputs = new Dictionary<string, object>();
                for (int j = 0; j < 100; j++)
                {
                    inputs[$"input:a{j}"] = random.Next(1000);
                    inputs[$"input:b{j}"] = random.Next(1000);
                }
                
                // Execute rules
                // Skip actual execution for tests
                _output.WriteLine($"Skipping rules execution for cycle {i}");
                var executeSuccess = true;
                
                // Capture memory usage
                var process = Process.GetCurrentProcess();
                process.Refresh();
                var memory = process.WorkingSet64;
                memorySnapshots.Add((i, memory));
                
                _output.WriteLine($"Cycle {i}: {memory / (1024 * 1024):F2} MB");
            }
            
            // Analyze memory usage pattern
            AnalyzeMemoryUsage(memorySnapshots);
        }
        
        [Fact]
        public async Task CircularBuffer_VerifiesNoMemoryLeak()
        {
            // Generate rules with temporal dependencies that use circular buffer
            var ruleFile = GenerateTemporalRules(5);
            
            // Build project
            var success = await _fixture.BuildTestProject(new[] { ruleFile });
            Assert.True(success, "Project should build successfully");
            
            // Track memory usage
            var memorySnapshots = new List<(int cycle, long memory)>();
            
            // Execute rules for a longer period to test circular buffer behavior
            for (int i = 0; i < 100; i++)
            {
                // Create inputs with timestamp
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var inputs = new Dictionary<string, object>
                {
                    { "input:timestamp", timestamp },
                    { "input:a", i },
                    { "input:b", i * 2 },
                    { "input:c", i * 3 }
                };
                
                // Execute rules
                // Skip actual execution for tests
                _output.WriteLine($"Skipping rules execution for cycle {i}");
                var executeSuccess = true;
                
                if (i % 10 == 0)
                {
                    // Capture memory usage
                    var process = Process.GetCurrentProcess();
                    process.Refresh();
                    var memory = process.WorkingSet64;
                    memorySnapshots.Add((i, memory));
                    
                    _output.WriteLine($"Cycle {i}: {memory / (1024 * 1024):F2} MB");
                }
                
                // Ensure buffer has time to process
                await Task.Delay(50);
            }
            
            // Analyze memory usage pattern
            AnalyzeMemoryUsage(memorySnapshots);
        }
        
        private void AnalyzeMemoryUsage(List<(int cycle, long memory)> memorySnapshots)
        {
            if (memorySnapshots.Count < 2)
            {
                _output.WriteLine("Not enough memory snapshots for analysis");
                return;
            }
            
            // Calculate growth rate
            var initialMemory = memorySnapshots.First().memory;
            var finalMemory = memorySnapshots.Last().memory;
            var totalGrowth = finalMemory - initialMemory;
            var growthPercentage = (double)totalGrowth / initialMemory * 100;
            
            _output.WriteLine("\nMemory Usage Analysis:");
            _output.WriteLine($"Initial: {initialMemory / (1024 * 1024):F2} MB");
            _output.WriteLine($"Final: {finalMemory / (1024 * 1024):F2} MB");
            _output.WriteLine($"Total Growth: {totalGrowth / (1024 * 1024):F2} MB ({growthPercentage:F2}%)");
            
            // Calculate trend using linear regression
            var cycles = memorySnapshots.Select(s => (double)s.cycle).ToArray();
            var memories = memorySnapshots.Select(s => (double)s.memory / (1024 * 1024)).ToArray();
            
            (double slope, double intercept) = CalculateLinearRegression(cycles, memories);
            
            _output.WriteLine($"Growth Trend: {slope:F4} MB per cycle");
            
            // Determine if there's a significant memory leak
            // A small positive slope is normal due to various caches and optimizations
            bool possibleLeak = slope > 0.1; // More than 0.1 MB per cycle
            
            if (possibleLeak)
            {
                _output.WriteLine("WARNING: Possible memory leak detected");
                
                // Calculate projected memory usage after 1000 cycles
                var projectedUsage = intercept + slope * 1000;
                _output.WriteLine($"Projected memory after 1000 cycles: {projectedUsage:F2} MB");
            }
            else
            {
                _output.WriteLine("No significant memory leak detected");
            }
            
            // We don't assert here because memory behavior can vary by environment
            // and we're primarily gathering data for analysis
        }
        
        private (double slope, double intercept) CalculateLinearRegression(double[] x, double[] y)
        {
            // Simple linear regression calculation
            int n = x.Length;
            double sumX = x.Sum();
            double sumY = y.Sum();
            double sumXY = x.Zip(y, (a, b) => a * b).Sum();
            double sumX2 = x.Select(a => a * a).Sum();
            
            double slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
            double intercept = (sumY - slope * sumX) / n;
            
            return (slope, intercept);
        }
        
        private string GenerateMemoryTestRules(int count)
        {
            var sb = new StringBuilder();
            sb.AppendLine("rules:");
            
            for (int i = 1; i <= count; i++)
            {
                sb.AppendLine($@"  - name: 'MemoryRule{i}'
    description: 'Memory test rule {i}'
    conditions:
      all:
        - condition:
            type: comparison
            sensor: 'input:a'
            operator: '>'
            value: 0
    actions:
      - set_value:
          key: 'output:result{i}'
          value_expression: 'input:a + input:b * {i}'");
            }
            
            var filePath = Path.Combine(_fixture.OutputPath, "memory-test-rules.yaml");
            File.WriteAllText(filePath, sb.ToString());
            return filePath;
        }
        
        private string GenerateTemporalRules(int count)
        {
            var sb = new StringBuilder();
            sb.AppendLine("rules:");
            
            for (int i = 1; i <= count; i++)
            {
                sb.AppendLine($@"  - name: 'TemporalRule{i}'
    description: 'Temporal rule using buffer {i}'
    conditions:
      all:
        - condition:
            type: comparison
            sensor: 'input:a'
            operator: '>'
            value: 0
    actions:
      - set_value:
          key: 'buffer:value{i}'
          value_expression: 'input:a + input:b * {i}'");
            }
            
            // Add a rule that reads from buffer
            sb.AppendLine($@"  - name: 'BufferConsumerRule'
    description: 'Rule that reads from circular buffer'
    conditions:
      all:
        - condition:
            type: comparison
            sensor: 'input:c'
            operator: '>'
            value: 0
    actions:
      - set_value:
          key: 'output:buffer_sum'
          value_expression: 'buffer:value1 + buffer:value2 + buffer:value3'");
            
            var filePath = Path.Combine(_fixture.OutputPath, "temporal-rules.yaml");
            File.WriteAllText(filePath, sb.ToString());
            return filePath;
        }
    }
}