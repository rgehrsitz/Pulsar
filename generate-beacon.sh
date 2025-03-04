#!/bin/bash

# Script to generate a Beacon AOT-compatible solution from Pulsar rule files

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

# Build the argument list
ARGS=("generate" "--rules" "$RULES_PATH" "--config" "$CONFIG_PATH" "--output" "$OUTPUT_PATH" "--target" "$TARGET")

if [ "$DEBUG" = true ]; then
    ARGS+=("--debug")
    echo "Debug mode enabled"
fi

# Run the Pulsar compiler
echo "Running Pulsar compiler..."
dotnet run --project Pulsar.Compiler -- "${ARGS[@]}"

if [ $? -ne 0 ]; then
    echo "Error: Pulsar compiler failed"
    exit 1
fi

# Build the Beacon solution
BEACON_DIR="$OUTPUT_PATH/Beacon"
if [ -d "$BEACON_DIR" ]; then
    echo "Building Beacon solution..."
    cd "$BEACON_DIR"
    dotnet build
    
    if [ $? -ne 0 ]; then
        echo "Error: Building Beacon solution failed"
        exit 1
    fi
    
    echo "Beacon solution built successfully"
    
    # Optionally publish as a standalone executable
    echo "Would you like to create a standalone executable? (y/n)"
    read -r PUBLISH
    
    if [[ "$PUBLISH" =~ ^[Yy]$ ]]; then
        echo "Publishing standalone executable for $TARGET..."
        dotnet publish Beacon.Runtime/Beacon.Runtime.csproj -c Release -r "$TARGET" --self-contained true
        
        if [ $? -ne 0 ]; then
            echo "Error: Publishing standalone executable failed"
            exit 1
        fi
        
        EXEC_PATH="Beacon.Runtime/bin/Release/net9.0/$TARGET/publish"
        echo "Standalone executable created at: $BEACON_DIR/$EXEC_PATH"
    fi
else
    echo "Error: Beacon solution directory not found at $BEACON_DIR"
    exit 1
fi

echo "Done!"