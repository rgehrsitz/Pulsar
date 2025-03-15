using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Beacon.PerformanceTester.Common
{
    /// <summary>
    /// Represents a pattern type for generating data
    /// </summary>
    public enum DataPatternType
    {
        Constant,
        Random,
        Stepped,
        Sinusoidal,
        Spike,
        Sequence,
        Ramp // Gradually increasing or decreasing value
        ,
    }

    /// <summary>
    /// Type of test case for specialized behaviors
    /// </summary>
    public enum TestCaseType
    {
        /// <summary>
        /// Standard test with constant settings throughout
        /// </summary>
        Standard,

        /// <summary>
        /// Test that gradually increases load over time
        /// </summary>
        RampUp,

        /// <summary>
        /// Long-running test for stability verification
        /// </summary>
        Stability,

        /// <summary>
        /// Test with sudden spike in load to measure response
        /// </summary>
        BurstLoad,

        /// <summary>
        /// Testing of maximum throughput capacity
        /// </summary>
        Saturation,
    }

    /// <summary>
    /// Timing model for collecting time series data during the test
    /// </summary>
    public enum TimeSeriesInterval
    {
        /// <summary>
        /// Collect data every second
        /// </summary>
        PerSecond,

        /// <summary>
        /// Collect data every 10 seconds
        /// </summary>
        Per10Seconds,

        /// <summary>
        /// Collect data every minute
        /// </summary>
        PerMinute,
    }

    /// <summary>
    /// Represents a test configuration for a sensor input
    /// </summary>
    public class SensorConfig
    {
        /// <summary>
        /// Key name for the sensor (e.g., "input:temperature")
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Pattern to use for generating data
        /// </summary>
        public DataPatternType PatternType { get; set; } = DataPatternType.Constant;

        /// <summary>
        /// Minimum value for the sensor
        /// </summary>
        public double MinValue { get; set; } = 0;

        /// <summary>
        /// Maximum value for the sensor
        /// </summary>
        public double MaxValue { get; set; } = 100;

        /// <summary>
        /// Constant value to use if pattern is Constant
        /// </summary>
        public double ConstantValue { get; set; } = 0;

        /// <summary>
        /// Rate of change per second for patterns like Stepped
        /// </summary>
        public double RateOfChange { get; set; } = 0;

        /// <summary>
        /// Period in seconds for oscillating patterns like Sinusoidal
        /// </summary>
        public double Period { get; set; } = 60;

        /// <summary>
        /// For sequence pattern, the specific values to cycle through
        /// </summary>
        public List<double>? Sequence { get; set; }

        /// <summary>
        /// Update frequency in milliseconds - how often to write to Redis
        /// </summary>
        public int UpdateFrequencyMs { get; set; } = 100;

        /// <summary>
        /// For ramp-up tests, the starting update frequency (ms)
        /// </summary>
        public int StartFrequencyMs { get; set; } = 1000;

        /// <summary>
        /// For ramp-up tests, the target update frequency (ms)
        /// </summary>
        public int TargetFrequencyMs { get; set; } = 10;

        /// <summary>
        /// For ramp-up tests, time to reach target frequency (seconds)
        /// </summary>
        public int RampDurationSeconds { get; set; } = 60;
    }

    /// <summary>
    /// Represents an expected output from a test
    /// </summary>
    public class ExpectedOutput
    {
        /// <summary>
        /// Key name for the output (e.g., "output:temperature_alert")
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Expected value for the output
        /// </summary>
        public object? Value { get; set; }

        /// <summary>
        /// Tolerance for numeric comparisons
        /// </summary>
        public double Tolerance { get; set; } = 0.0001;

        /// <summary>
        /// Expected time to reach this value in milliseconds (from test start)
        /// </summary>
        public int ExpectedTimeToValueMs { get; set; } = 0;

        /// <summary>
        /// Maximum allowed latency in milliseconds
        /// </summary>
        public int MaxLatencyMs { get; set; } = 100;

        /// <summary>
        /// For pass/fail tests, whether this is a critical output
        /// </summary>
        public bool IsCritical { get; set; } = true;
    }

    /// <summary>
    /// Performance success criteria for test cases
    /// </summary>
    public class PerformanceCriteria
    {
        /// <summary>
        /// Maximum acceptable average latency in milliseconds
        /// </summary>
        public double MaxAvgLatencyMs { get; set; } = 100;

        /// <summary>
        /// Maximum acceptable 95th percentile latency in milliseconds
        /// </summary>
        public double MaxP95LatencyMs { get; set; } = 200;

        /// <summary>
        /// Maximum acceptable CPU usage percentage
        /// </summary>
        public double MaxCpuPercent { get; set; } = 80;

        /// <summary>
        /// Maximum acceptable memory usage in MB
        /// </summary>
        public double MaxMemoryMB { get; set; } = 500;

        /// <summary>
        /// Maximum rate of missed/late detections as percentage
        /// </summary>
        public double MaxErrorRate { get; set; } = 0.1;
    }

    /// <summary>
    /// Configuration for a test phase (e.g., warmup, main test, cooldown)
    /// </summary>
    public class TestPhase
    {
        /// <summary>
        /// Name of the phase
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Duration of this phase in seconds
        /// </summary>
        public int DurationSeconds { get; set; } = 30;

        /// <summary>
        /// Input configuration for this phase
        /// </summary>
        public List<SensorConfig> Inputs { get; set; } = new List<SensorConfig>();

        /// <summary>
        /// Whether to collect metrics during this phase
        /// </summary>
        public bool CollectMetrics { get; set; } = true;
    }

    /// <summary>
    /// Represents a test case for Beacon performance testing
    /// </summary>
    public class TestCase
    {
        /// <summary>
        /// Name of the test case
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Description of the test case
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Type of test case
        /// </summary>
        public TestCaseType Type { get; set; } = TestCaseType.Standard;

        /// <summary>
        /// Test duration in seconds
        /// </summary>
        public int DurationSeconds { get; set; } = 60;

        /// <summary>
        /// Input sensors to simulate
        /// </summary>
        public List<SensorConfig> Inputs { get; set; } = new List<SensorConfig>();

        /// <summary>
        /// Expected outputs to verify
        /// </summary>
        public List<ExpectedOutput> ExpectedOutputs { get; set; } = new List<ExpectedOutput>();

        /// <summary>
        /// Tags for categorizing tests
        /// </summary>
        public List<string> Tags { get; set; } = new List<string>();

        /// <summary>
        /// Success criteria for this test
        /// </summary>
        public PerformanceCriteria? SuccessCriteria { get; set; }

        /// <summary>
        /// For multi-phase tests, the test phases to execute
        /// </summary>
        public List<TestPhase>? Phases { get; set; }

        /// <summary>
        /// How frequently to collect time series data
        /// </summary>
        public TimeSeriesInterval DataCollectionInterval { get; set; } =
            TimeSeriesInterval.PerSecond;

        /// <summary>
        /// Whether to perform warmup before the main test
        /// </summary>
        public bool PerformWarmup { get; set; } = false;

        /// <summary>
        /// Duration of warmup in seconds, if enabled
        /// </summary>
        public int WarmupDurationSeconds { get; set; } = 10;
    }

    /// <summary>
    /// Represents a test scenario that may contain multiple test cases
    /// </summary>
    public class TestScenario
    {
        /// <summary>
        /// Name of the test scenario
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Description of the test scenario
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Test cases to run as part of this scenario
        /// </summary>
        public List<TestCase> TestCases { get; set; } = new List<TestCase>();

        /// <summary>
        /// Optional delay between test cases in seconds
        /// </summary>
        public int DelayBetweenTestsSeconds { get; set; } = 5;

        /// <summary>
        /// Whether to abort the scenario if a test case fails
        /// </summary>
        public bool AbortOnFailure { get; set; } = false;

        /// <summary>
        /// Environment information for tracking tests
        /// </summary>
        public Dictionary<string, string>? Environment { get; set; }

        /// <summary>
        /// Whether to check Redis health before beginning tests
        /// </summary>
        public bool VerifyRedisConnection { get; set; } = true;

        /// <summary>
        /// Whether to verify the Beacon process exists before beginning
        /// </summary>
        public bool VerifyBeaconProcess { get; set; } = true;
    }

    /// <summary>
    /// Time series data point for performance metrics over time
    /// </summary>
    public class TimeSeriesDataPoint
    {
        /// <summary>
        /// Elapsed time in milliseconds since start of test
        /// </summary>
        public double TimeMs { get; set; }

        /// <summary>
        /// Latency measured at this point in milliseconds
        /// </summary>
        public double LatencyMs { get; set; }

        /// <summary>
        /// CPU usage percentage at this point
        /// </summary>
        public double CpuPercent { get; set; }

        /// <summary>
        /// Memory usage in MB at this point
        /// </summary>
        public double MemoryMB { get; set; }

        /// <summary>
        /// Input rate at this point (values per second)
        /// </summary>
        public double InputRatePerSec { get; set; }

        /// <summary>
        /// Output rate at this point (values per second)
        /// </summary>
        public double OutputRatePerSec { get; set; }
    }

    /// <summary>
    /// Represents a test result for a single output value
    /// </summary>
    public class OutputResult
    {
        /// <summary>
        /// Key name for the output
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Actual value that was observed
        /// </summary>
        public object? ActualValue { get; set; }

        /// <summary>
        /// Expected value from the test configuration
        /// </summary>
        public object? ExpectedValue { get; set; }

        /// <summary>
        /// Whether the value matched the expected value
        /// </summary>
        public bool IsMatch { get; set; }

        /// <summary>
        /// Time taken to reach this value in milliseconds from the test start
        /// </summary>
        public double TimeTakenMs { get; set; }

        /// <summary>
        /// End-to-end latency from input injection to output detection
        /// </summary>
        public double LatencyMs { get; set; }
    }

    /// <summary>
    /// Represents the result of a test case
    /// </summary>
    public class TestCaseResult
    {
        /// <summary>
        /// Name of the test case
        /// </summary>
        public string TestCaseName { get; set; } = string.Empty;

        /// <summary>
        /// Overall success flag
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Results for each expected output
        /// </summary>
        public List<OutputResult> OutputResults { get; set; } = new List<OutputResult>();

        /// <summary>
        /// Test duration in milliseconds
        /// </summary>
        public double DurationMs { get; set; }

        /// <summary>
        /// Average latency across all outputs in milliseconds
        /// </summary>
        public double AverageLatencyMs { get; set; }

        /// <summary>
        /// 95th percentile latency in milliseconds
        /// </summary>
        public double P95LatencyMs { get; set; }

        /// <summary>
        /// Maximum latency observed in milliseconds
        /// </summary>
        public double MaxLatencyMs { get; set; }

        /// <summary>
        /// Peak memory usage of the Beacon process in MB
        /// </summary>
        public double PeakMemoryMB { get; set; }

        /// <summary>
        /// Average CPU usage of the Beacon process
        /// </summary>
        public double AvgCpuPercent { get; set; }

        /// <summary>
        /// Error rate as a percentage (outputs that didn't match expected values)
        /// </summary>
        public double ErrorRatePercent { get; set; }

        /// <summary>
        /// Time series data for the test case
        /// </summary>
        public List<TimeSeriesDataPoint>? TimeSeriesData { get; set; }

        /// <summary>
        /// Input rate in operations per second
        /// </summary>
        public double InputRatePerSecond { get; set; }

        /// <summary>
        /// Time series for latency as arrays (time, value) for easy plotting
        /// </summary>
        public Tuple<double[], double[]>? LatencyTimeSeries { get; set; }

        /// <summary>
        /// Time series for CPU usage as arrays (time, value) for easy plotting
        /// </summary>
        public Tuple<double[], double[]>? CpuTimeSeries { get; set; }

        /// <summary>
        /// Time series for memory usage as arrays (time, value) for easy plotting
        /// </summary>
        public Tuple<double[], double[]>? MemoryTimeSeries { get; set; }
    }

    /// <summary>
    /// Represents the result of a test scenario
    /// </summary>
    public class TestScenarioResult
    {
        /// <summary>
        /// Name of the test scenario
        /// </summary>
        public string ScenarioName { get; set; } = string.Empty;

        /// <summary>
        /// Overall success flag
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Results for each test case
        /// </summary>
        public List<TestCaseResult> TestCaseResults { get; set; } = new List<TestCaseResult>();

        /// <summary>
        /// Start time of the scenario
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// End time of the scenario
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// Total duration in milliseconds
        /// </summary>
        public double TotalDurationMs { get; set; }

        /// <summary>
        /// Average latency across all test cases
        /// </summary>
        public double AverageLatencyMs { get; set; }

        /// <summary>
        /// Peak latency across all test cases
        /// </summary>
        public double PeakLatencyMs { get; set; }

        /// <summary>
        /// Peak CPU usage across all test cases
        /// </summary>
        public double PeakCpuPercent { get; set; }

        /// <summary>
        /// Peak memory usage across all test cases
        /// </summary>
        public double PeakMemoryMB { get; set; }

        /// <summary>
        /// Environment information at time of test
        /// </summary>
        public Dictionary<string, string>? Environment { get; set; }

        /// <summary>
        /// Unique ID for this test run
        /// </summary>
        public string TestRunId { get; set; } = Guid.NewGuid().ToString();
    }
}
