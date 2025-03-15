using System;

namespace Beacon.PerformanceTester.InputGenerator.Services
{
    /// <summary>
    /// Interface for pattern generators that produce sensor data
    /// </summary>
    public interface IPatternGenerator
    {
        /// <summary>
        /// Generate a value at a specific point in time
        /// </summary>
        /// <param name="timeElapsedMs">Milliseconds elapsed since test start</param>
        /// <returns>The generated value</returns>
        double GenerateValue(long timeElapsedMs);
    }

    /// <summary>
    /// Factory for creating pattern generators based on configuration
    /// </summary>
    public interface IPatternGeneratorFactory
    {
        /// <summary>
        /// Create a pattern generator for the given sensor configuration
        /// </summary>
        /// <param name="sensorConfig">The sensor configuration</param>
        /// <returns>A pattern generator instance</returns>
        IPatternGenerator CreateGenerator(Common.SensorConfig sensorConfig);
    }
}
