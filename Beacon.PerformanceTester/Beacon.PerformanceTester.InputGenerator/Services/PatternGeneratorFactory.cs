using System;
using System.Collections.Generic;
using System.Linq;
using Beacon.PerformanceTester.Common;
using Microsoft.Extensions.Logging;

namespace Beacon.PerformanceTester.InputGenerator.Services
{
    /// <summary>
    /// Factory for creating pattern generators based on configuration
    /// </summary>
    public class PatternGeneratorFactory : IPatternGeneratorFactory
    {
        private readonly ILogger<PatternGeneratorFactory> _logger;
        private readonly Random _random;

        public PatternGeneratorFactory(ILogger<PatternGeneratorFactory> logger)
        {
            _logger = logger;
            _random = new Random();
        }

        /// <summary>
        /// Create a pattern generator based on the sensor configuration
        /// </summary>
        public IPatternGenerator CreateGenerator(SensorConfig sensorConfig)
        {
            _logger.LogInformation(
                "Creating pattern generator for {Key} with pattern type {PatternType}",
                sensorConfig.Key,
                sensorConfig.PatternType
            );

            return sensorConfig.PatternType switch
            {
                DataPatternType.Constant => new ConstantGenerator(sensorConfig.ConstantValue),
                DataPatternType.Random => new RandomGenerator(
                    sensorConfig.MinValue,
                    sensorConfig.MaxValue,
                    _random
                ),
                DataPatternType.Stepped => new SteppedGenerator(
                    sensorConfig.MinValue,
                    sensorConfig.MaxValue,
                    sensorConfig.RateOfChange
                ),
                DataPatternType.Sinusoidal => new SinusoidalGenerator(
                    sensorConfig.MinValue,
                    sensorConfig.MaxValue,
                    sensorConfig.Period * 1000
                ),
                DataPatternType.Spike => new SpikeGenerator(
                    sensorConfig.MinValue,
                    sensorConfig.MaxValue,
                    sensorConfig.Period * 1000
                ),
                DataPatternType.Sequence => new SequenceGenerator(
                    sensorConfig.Sequence ?? new List<double>()
                ),
                DataPatternType.Ramp => new RampGenerator(
                    sensorConfig.MinValue,
                    sensorConfig.MaxValue,
                    sensorConfig.RampDurationSeconds * 1000
                ),
                _ => throw new ArgumentException(
                    $"Unsupported pattern type: {sensorConfig.PatternType}"
                ),
            };
        }
    }

    /// <summary>
    /// Generates a constant value
    /// </summary>
    public class ConstantGenerator : IPatternGenerator
    {
        private readonly double _value;

        public ConstantGenerator(double value)
        {
            _value = value;
        }

        public double GenerateValue(long timeElapsedMs) => _value;
    }

    /// <summary>
    /// Generates random values within a range
    /// </summary>
    public class RandomGenerator : IPatternGenerator
    {
        private readonly double _min;
        private readonly double _max;
        private readonly Random _random;

        public RandomGenerator(double min, double max, Random random)
        {
            _min = min;
            _max = max;
            _random = random;
        }

        public double GenerateValue(long timeElapsedMs)
        {
            return _min + (_random.NextDouble() * (_max - _min));
        }
    }

    /// <summary>
    /// Generates values that increase or decrease steadily over time
    /// </summary>
    public class SteppedGenerator : IPatternGenerator
    {
        private readonly double _min;
        private readonly double _max;
        private readonly double _ratePerMs;
        private double _currentValue;
        private bool _increasing = true;

        public SteppedGenerator(double min, double max, double ratePerSecond)
        {
            _min = min;
            _max = max;
            _ratePerMs = ratePerSecond / 1000.0; // Convert to rate per ms
            _currentValue = min;
        }

        public double GenerateValue(long timeElapsedMs)
        {
            // Calculate new value based on time
            double newValue = _currentValue + (_increasing ? _ratePerMs : -_ratePerMs);

            // Check bounds and reverse direction if needed
            if (newValue >= _max)
            {
                newValue = _max;
                _increasing = false;
            }
            else if (newValue <= _min)
            {
                newValue = _min;
                _increasing = true;
            }

            _currentValue = newValue;
            return newValue;
        }
    }

    /// <summary>
    /// Generates sinusoidal wave pattern
    /// </summary>
    public class SinusoidalGenerator : IPatternGenerator
    {
        private readonly double _min;
        private readonly double _max;
        private readonly double _periodMs;

        public SinusoidalGenerator(double min, double max, double periodMs)
        {
            _min = min;
            _max = max;
            _periodMs = periodMs;
        }

        public double GenerateValue(long timeElapsedMs)
        {
            // Calculate phase (0 to 2π) based on time and period
            double phase = (2 * Math.PI * timeElapsedMs) / _periodMs;

            // Calculate sine wave value (-1 to 1)
            double sineValue = Math.Sin(phase);

            // Scale and shift to desired range
            double amplitude = (_max - _min) / 2;
            double offset = _min + amplitude;
            return offset + (amplitude * sineValue);
        }
    }

    /// <summary>
    /// Generates spike patterns at regular intervals
    /// </summary>
    public class SpikeGenerator : IPatternGenerator
    {
        private readonly double _min;
        private readonly double _max;
        private readonly double _periodMs;

        public SpikeGenerator(double min, double max, double periodMs)
        {
            _min = min;
            _max = max;
            _periodMs = periodMs;
        }

        public double GenerateValue(long timeElapsedMs)
        {
            // Calculate where we are in the current period (0 to 1)
            double periodPosition = (timeElapsedMs % _periodMs) / _periodMs;

            // Spike occurs in the first 5% of the period
            if (periodPosition < 0.05)
            {
                // Triangle wave for the spike
                double spikePosition = periodPosition / 0.05;
                double spikeValue =
                    spikePosition < 0.5 ? spikePosition * 2 : 2 - (spikePosition * 2);

                // Scale to desired range
                return _min + ((_max - _min) * spikeValue);
            }

            // Otherwise return min value
            return _min;
        }
    }

    /// <summary>
    /// Cycles through a predefined sequence of values
    /// </summary>
    public class SequenceGenerator : IPatternGenerator
    {
        private readonly List<double> _sequence;

        public SequenceGenerator(List<double> sequence)
        {
            _sequence = sequence.Count > 0 ? sequence : new List<double> { 0 };
        }

        public double GenerateValue(long timeElapsedMs)
        {
            // If sequence is empty, return 0
            if (_sequence.Count == 0)
                return 0;

            // Simple implementation: cycle through the sequence based on time
            // For more complex uses, this could be refined to account for specific timing patterns
            int index = (int)(timeElapsedMs / 1000) % _sequence.Count;
            return _sequence[index];
        }
    }

    /// <summary>
    /// Generates values that ramp linearly from min to max over a specified duration
    /// </summary>
    public class RampGenerator : IPatternGenerator
    {
        private readonly double _min;
        private readonly double _max;
        private readonly double _durationMs;

        public RampGenerator(double min, double max, double durationMs)
        {
            _min = min;
            _max = max;
            _durationMs = durationMs > 0 ? durationMs : 1000; // Ensure non-zero duration
        }

        public double GenerateValue(long timeElapsedMs)
        {
            // Calculate progress as a ratio (0 to 1)
            double progress = Math.Min(timeElapsedMs / _durationMs, 1.0);

            // Linear interpolation from min to max
            return _min + (progress * (_max - _min));
        }
    }
}
