#!/bin/bash

# Script to generate a Beacon AOT-compatible solution from Pulsar rule files
# Updated to use the fixed implementation

# Check that required commands exist
if ! command -v dotnet &> /dev/null; then
    echo "dotnet is not installed or not in the PATH"
    exit 1
fi

# Default values
RULES_PATH="./TestRules.yaml"
CONFIG_PATH="./system_config.yaml"
OUTPUT_PATH="./BeaconOutput"
TARGET="linux-x64"
DEBUG=false

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --rules)
            RULES_PATH="$2"
            shift 2
            ;;
        --config)
            CONFIG_PATH="$2"
            shift 2
            ;;
        --output)
            OUTPUT_PATH="$2"
            shift 2
            ;;
        --target)
            TARGET="$2"
            shift 2
            ;;
        --debug)
            DEBUG=true
            shift
            ;;
        --help)
            echo "Usage: $0 [options]"
            echo ""
            echo "Options:"
            echo "  --rules PATH    Path to rules file or directory (default: ./TestRules.yaml)"
            echo "  --config PATH   Path to system config file (default: ./system_config.yaml)"
            echo "  --output PATH   Output directory (default: ./BeaconOutput)"
            echo "  --target RID    Target runtime identifier (default: linux-x64)"
            echo "  --debug         Generate debug symbols and information"
            echo "  --help          Show this help message"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            echo "Use --help for usage information"
            exit 1
            ;;
    esac
done

# Validate input paths
if [ ! -e "$RULES_PATH" ]; then
    echo "Rules path does not exist: $RULES_PATH"
    exit 1
fi

if [ ! -e "$CONFIG_PATH" ]; then
    echo "Config path does not exist: $CONFIG_PATH"
    exit 1
fi

# Create output directory if it doesn't exist
mkdir -p "$OUTPUT_PATH"

echo "Generating Beacon solution from rules at $RULES_PATH"
echo "Using config file: $CONFIG_PATH"
echo "Output path: $OUTPUT_PATH"
echo "Target platform: $TARGET"

# Create a C# script to run the fixed implementation
cat > "$OUTPUT_PATH/generate-beacon.csx" << EOL
// Simple script to generate a Beacon solution using the fixed implementation

#r "System.IO"
#r "System.Threading.Tasks"
#r "/home/robertg/Pulsar/Pulsar.Compiler/bin/Debug/net9.0/Pulsar.Compiler.dll"

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Pulsar.Compiler.Config;
using Pulsar.Compiler.Models;
using Pulsar.Compiler.Parsers;

async Task<int> Main()
{
    try
    {
        Console.WriteLine("Generating AOT-compatible Beacon solution...");
        
        string rulesPath = "$RULES_PATH";
        string configPath = "$CONFIG_PATH";
        string outputPath = "$OUTPUT_PATH";
        string target = "$TARGET";
        bool debug = $DEBUG;
        
        // Parse system config
        string configContent = await File.ReadAllTextAsync(configPath);
        var configLoader = new Pulsar.Compiler.Config.ConfigurationLoader();
        var systemConfig = configLoader.LoadFromYaml(configContent);
        
        Console.WriteLine($"System configuration loaded with {systemConfig.ValidSensors.Count} valid sensors");
        
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
            Console.Error.WriteLine($"Rules path not found: {rulesPath}");
            return 1;
        }
        
        Console.WriteLine($"Parsed {rules.Count} rules");
        
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
            GenerateDebugInfo = debug,
            OptimizeOutput = !debug,
            Namespace = "Beacon.Runtime",
            RedisConnection = systemConfig.Redis.Endpoints.Count > 0 ? 
                systemConfig.Redis.Endpoints[0] : "localhost:6379",
            CycleTime = systemConfig.CycleTime,
            BufferCapacity = systemConfig.BufferCapacity
        };
        
        // Use the BeaconBuildOrchestratorFixed
        var orchestrator = new BeaconBuildOrchestratorFixed();
        var result = await orchestrator.BuildBeaconAsync(buildConfig);
        
        if (result.Success)
        {
            Console.WriteLine($"Beacon solution generated successfully at: {Path.Combine(outputPath, "Beacon")}");
            return 0;
        }
        else
        {
            Console.Error.WriteLine("Failed to generate Beacon solution:");
            foreach (var error in result.Errors)
            {
                Console.Error.WriteLine($"  {error}");
            }
            return 1;
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error generating Beacon solution: {ex.Message}");
        return 1;
    }
}

await Main();
EOL

# Compile the fixed implementation
echo "Building Pulsar.Compiler..."
dotnet build

# Run the script to generate the Beacon solution
echo "Running generation script..."
dotnet script "$OUTPUT_PATH/generate-beacon.csx"

if [ $? -eq 0 ]; then
    echo "Beacon solution created successfully!"
    
    # Optionally build the generated solution
    if [ -d "$OUTPUT_PATH/Beacon" ]; then
        echo "Would you like to build the Beacon solution? (y/n)"
        read -r BUILD
        
        if [[ "$BUILD" =~ ^[Yy]$ ]]; then
            cd "$OUTPUT_PATH/Beacon"
            dotnet build
            
            if [ $? -eq 0 ]; then
                echo "Build successful!"
            else
                echo "Build failed"
            fi
        fi
    fi
else
    echo "Failed to generate Beacon solution"
    exit 1
fi

echo "Done!"