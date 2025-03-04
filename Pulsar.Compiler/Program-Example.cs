// File: Program-Example.cs - Example of using BeaconBuildOrchestratorFixed
// This file shows how to integrate the fixed Beacon implementation
// Copy relevant sections to your actual Program.cs when ready

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Pulsar.Compiler.Config;
using Pulsar.Compiler.Models;
using Pulsar.Compiler.Parsers;
using Serilog;

namespace Pulsar.Compiler.Examples
{
    public class ProgramExample
    {
        private static readonly ILogger _logger = LoggingConfig.GetLogger().ForContext<ProgramExample>();

        public static async Task GenerateBeaconSolution(string rulesPath, string configPath, string outputPath, string target)
        {
            try
            {
                _logger.Information("Generating AOT-compatible Beacon solution...");

                // Parse system config
                if (!File.Exists(configPath))
                {
                    _logger.Error("System configuration file not found: {Path}", configPath);
                    return;
                }
                
                var systemConfig = SystemConfig.Load(configPath);
                
                _logger.Information("System configuration loaded with {SensorCount} valid sensors", 
                    systemConfig.ValidSensors.Count);

                // Parse rules
                var parser = new DslParser();
                var rules = new List<RuleDefinition>();

                if (File.Exists(rulesPath))
                {
                    var content = await File.ReadAllTextAsync(rulesPath);
                    var parsedRules = parser.ParseRules(content, systemConfig.ValidSensors, Path.GetFileName(rulesPath));
                    rules.AddRange(parsedRules);
                }
                else if (Directory.Exists(rulesPath))
                {
                    foreach (var file in Directory.GetFiles(rulesPath, "*.yaml", SearchOption.AllDirectories))
                    {
                        var content = await File.ReadAllTextAsync(file);
                        var parsedRules = parser.ParseRules(content, systemConfig.ValidSensors, Path.GetFileName(file));
                        rules.AddRange(parsedRules);
                    }
                }
                else
                {
                    _logger.Error("Rules path not found: {Path}", rulesPath);
                    return;
                }

                _logger.Information("Parsed {Count} rules", rules.Count);

                // Create build config
                var buildConfig = new BuildConfig
                {
                    OutputPath = outputPath,
                    Target = target,
                    ProjectName = "Beacon.Runtime",
                    AssemblyName = "Beacon.Runtime",
                    TargetFramework = "net9.0",
                    RulesPath = rulesPath,
                    RuleDefinitions = rules,
                    SystemConfig = systemConfig,
                    StandaloneExecutable = true,
                    GenerateDebugInfo = false,
                    OptimizeOutput = true,
                    Namespace = "Beacon.Runtime",
                    RedisConnection = systemConfig.Redis.Endpoints.Count > 0 ? 
                        systemConfig.Redis.Endpoints[0] : "localhost:6379",
                    CycleTime = systemConfig.CycleTime,
                    BufferCapacity = systemConfig.BufferCapacity,
                    MaxRulesPerFile = 50,
                    MaxLinesPerFile = 1000,
                    ComplexityThreshold = 10,
                    GroupParallelRules = true
                };

                // Use the fixed BeaconBuildOrchestrator
                var orchestrator = new BeaconBuildOrchestratorFixed();
                var result = await orchestrator.BuildBeaconAsync(buildConfig);

                if (result.Success)
                {
                    _logger.Information("Beacon solution generated successfully at: {Path}", 
                        Path.Combine(outputPath, "Beacon"));
                    
                    foreach (var file in result.GeneratedFiles)
                    {
                        _logger.Debug("Generated file: {File}", file);
                    }
                }
                else
                {
                    _logger.Error("Failed to generate Beacon solution:");
                    foreach (var error in result.Errors)
                    {
                        _logger.Error("  {Error}", error);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error generating Beacon solution");
            }
        }
    }
}