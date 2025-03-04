# Beacon Implementation Guide

## Overview

This guide explains how to implement the transition from Pulsar.Runtime to a fully AOT-compatible Beacon solution as described in the AOT Plan. The implementation separates the Beacon solution into its own directory structure, fully independent of the Pulsar compilation process.

## Key Components

1. **BeaconTemplateManager**: Generates the complete Beacon solution structure
2. **BeaconBuildOrchestrator**: Orchestrates the build process for the Beacon solution
3. **Updates to Program.cs**: Adds new commands for generating Beacon solutions

## Implementation Steps

### Step 1: Create Required Helper Classes

1. Create `BeaconTemplateManager.cs` in the Pulsar.Compiler/Config directory
2. Create `BeaconBuildOrchestrator.cs` in the Pulsar.Compiler/Config directory

### Step 2: Update the Program Command Logic

Modify the `GenerateBuildableProject` method in Program.cs to use the new BeaconBuildOrchestrator:

```csharp
public static async Task<bool> GenerateBuildableProject(
    Dictionary<string, string> options,
    ILogger logger
)
{
    logger.Information("Generating AOT-compatible Beacon solution...");

    try
    {
        var buildConfig = CreateBuildConfig(options);
        var systemConfig = await LoadSystemConfig(
            options.GetValueOrDefault("config", "system_config.yaml")
        );
        logger.Information(
            "System configuration loaded. Valid sensors: {ValidSensors}",
            string.Join(", ", systemConfig.ValidSensors)
        );

        // Parse rules from files
        var parser = new DslParser();
        var rules = new List<RuleDefinition>();
        string rulesPath = options["rules"];

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
            logger.Error("Rules path not found: {Path}", rulesPath);
            return false;
        }

        logger.Information("Parsed {Count} rules from {Path}", rules.Count, rulesPath);

        // Set up the enhanced build config for Beacon
        buildConfig.ProjectName = "Beacon.Runtime";
        buildConfig.AssemblyName = "Beacon.Runtime";
        buildConfig.Namespace = "Beacon.Runtime";
        buildConfig.SolutionName = "Beacon";
        buildConfig.RuleDefinitions = rules;
        buildConfig.SystemConfig = systemConfig;
        buildConfig.CreateSeparateDirectory = true;
        buildConfig.GenerateTestProject = true;
        buildConfig.RedisConnection = systemConfig.Redis.Endpoints.Count > 0 ? systemConfig.Redis.Endpoints[0] : "localhost:6379";
        buildConfig.CycleTime = systemConfig.CycleTime;
        buildConfig.BufferCapacity = systemConfig.BufferCapacity;

        // Use the new BeaconBuildOrchestrator
        var orchestrator = new BeaconBuildOrchestrator();
        var result = await orchestrator.BuildBeaconAsync(buildConfig);
        
        if (!result.Success)
        {
            foreach (var error in result.Errors)
            {
                logger.Error(error);
            }
            return false;
        }
        
        logger.Information("Generated Beacon solution at: {Path}", 
            Path.Combine(buildConfig.OutputPath, buildConfig.SolutionName));
        
        foreach (var file in result.GeneratedFiles)
        {
            logger.Debug("Generated file: {File}", file);
        }

        return true;
    }
    catch (Exception ex)
    {
        logger.Error(ex, "Failed to generate buildable project");
        return false;
    }
}
```

### Step 3: Update the Test Fixtures

1. Update RuntimeValidationFixture to handle the new Beacon solution structure
2. Modify the BuildTestProject method to use the BeaconBuildOrchestrator

## Directory Structure

The new Beacon solution will have the following structure:

```
/Beacon/
├── Beacon.sln
├── Beacon.Runtime/
│   ├── Beacon.Runtime.csproj
│   ├── Program.cs
│   ├── RuntimeOrchestrator.cs
│   ├── RuntimeConfig.cs
│   ├── Generated/
│   │   ├── RuleGroup0.cs
│   │   ├── RuleGroup1.cs
│   │   ├── RuleCoordinator.cs
│   │   └── rules.manifest.json
│   ├── Services/
│   │   ├── RedisConfiguration.cs
│   │   ├── RedisService.cs
│   │   ├── RedisMonitoring.cs
│   │   └── RedisLoggingConfiguration.cs
│   ├── Buffers/
│   │   ├── CircularBuffer.cs
│   │   ├── IDateTimeProvider.cs
│   │   └── SystemDateTimeProvider.cs
│   └── Interfaces/
│       ├── ICompiledRules.cs
│       ├── IRuleCoordinator.cs
│       └── IRuleGroup.cs
└── Beacon.Tests/
    ├── Beacon.Tests.csproj
    ├── BasicRuntimeTests.cs
    ├── Generated/
    └── Fixtures/
        └── RuntimeTestFixture.cs
```

## How to Use

To generate a Beacon solution, run:

```bash
dotnet run --project Pulsar.Compiler -- generate --rules ./TestRules.yaml --config system_config.yaml --output ./BeaconOutput
```

To build the generated solution:

```bash
cd BeaconOutput/Beacon
dotnet build
```

To build a standalone executable:

```bash
cd BeaconOutput/Beacon
dotnet publish -c Release -r linux-x64 --self-contained true
```

## AOT Compatibility

The generated Beacon solution is fully AOT-compatible, with the following enhancements:

1. Proper AOT compatibility attributes in Program.cs
2. Trimming configuration in trimming.xml
3. No dynamic code generation or reflection
4. Runtime services implemented using interfaces and dependency injection
5. Circular buffer implementation for temporal rules

## Testing

The Beacon.Tests project includes:

1. Basic runtime tests
2. Redis integration tests using TestContainers
3. Test fixtures for runtime validation

## Conclusion

This implementation fulfills the goals of the AOT Plan by creating a separate, standalone Beacon solution that is fully AOT-compatible and can be deployed independently.