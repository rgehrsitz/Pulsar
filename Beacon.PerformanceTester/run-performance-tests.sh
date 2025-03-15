#!/bin/bash

# Beacon Performance Tester - Runner Script

# Default values
SCENARIO_FILE="./Beacon.PerformanceTester.InputGenerator/test-scenarios.json"
REDIS_CONNECTION="localhost:6379"
OUTPUT_FILE=""
REPORT_DIR="./reports"
DURATION_OVERRIDE=""
PROCESS_NAME="Beacon"
MONITOR_PORT="5050"
VISUALIZE="false"

# Parse command line arguments
while [[ "$#" -gt 0 ]]; do
    case $1 in
        --scenario|-s) SCENARIO_FILE="$2"; shift ;;
        --redis|-r) REDIS_CONNECTION="$2"; shift ;;
        --output|-o) OUTPUT_FILE="$2"; shift ;;
        --report-dir) REPORT_DIR="$2"; shift ;;
        --duration|-d) DURATION_OVERRIDE="$2"; shift ;;
        --process|-p) PROCESS_NAME="$2"; shift ;;
        --port) MONITOR_PORT="$2"; shift ;;
        --visualize|-v) VISUALIZE="true" ;;
        --help|-h) 
            echo "Beacon Performance Tester"
            echo ""
            echo "Usage: $0 [options]"
            echo ""
            echo "Options:"
            echo "  --scenario, -s FILE      Specify the test scenario JSON file"
            echo "  --redis, -r HOST:PORT    Redis connection string (default: localhost:6379)"
            echo "  --output, -o FILE        Output file for test results (JSON format)"
            echo "  --report-dir DIR         Directory for visualization reports (default: ./reports)"
            echo "  --duration, -d SECONDS   Override the test duration for all test cases"
            echo "  --process, -p NAME       Name of the Beacon process to monitor (default: Beacon)"
            echo "  --port PORT              Port for communication between components (default: 5050)"
            echo "  --visualize, -v          Generate visualization reports"
            echo "  --help, -h               Show this help message"
            exit 0
            ;;
        *) echo "Unknown parameter: $1"; exit 1 ;;
    esac
    shift
done

# Check if Beacon process is running
if ! pgrep -x "$PROCESS_NAME" > /dev/null; then
    echo "Warning: Beacon process '$PROCESS_NAME' not found. Process monitoring will be disabled."
fi

# Ensure Redis is running
echo "Checking Redis server..."
if ! command -v redis-cli &> /dev/null || ! redis-cli ping &> /dev/null; then
    echo "Redis server not running. Starting Redis with Docker..."
    docker run -d --name beacon-perf-redis -p 6379:6379 redis:latest
    sleep 2
    if ! redis-cli ping &> /dev/null; then
        echo "Failed to start Redis. Please start Redis manually."
        exit 1
    fi
    echo "Redis started successfully."
else
    echo "Redis is already running."
fi

# Build the performance tester
echo "Building Beacon Performance Tester..."
dotnet build

# Clear any existing test data
echo "Clearing existing Redis data..."
redis-cli keys "input:*" | xargs -r redis-cli del
redis-cli keys "output:*" | xargs -r redis-cli del
redis-cli keys "buffer:*" | xargs -r redis-cli del

# Prepare arguments for the components
COMMON_ARGS="--Redis:ConnectionString=$REDIS_CONNECTION"
OUTPUT_MONITOR_ARGS="--Monitor:Port=$MONITOR_PORT --Monitor:ProcessName=$PROCESS_NAME"
INPUT_GENERATOR_ARGS="--scenarioFile=$SCENARIO_FILE --OutputMonitor:Port=$MONITOR_PORT"

if [ ! -z "$DURATION_OVERRIDE" ]; then
    INPUT_GENERATOR_ARGS="$INPUT_GENERATOR_ARGS --DurationOverride=$DURATION_OVERRIDE"
fi

if [ ! -z "$OUTPUT_FILE" ]; then
    INPUT_GENERATOR_ARGS="$INPUT_GENERATOR_ARGS --outputFile=$OUTPUT_FILE"
fi

# Start the OutputMonitor in background
echo "Starting Output Monitor..."
(cd Beacon.PerformanceTester.OutputMonitor && dotnet run $COMMON_ARGS $OUTPUT_MONITOR_ARGS &)
OUTPUT_MONITOR_PID=$!

# Wait briefly for the OutputMonitor to start
sleep 2

# Run the InputGenerator
echo "Running Input Generator..."
cd Beacon.PerformanceTester.InputGenerator
dotnet run $COMMON_ARGS $INPUT_GENERATOR_ARGS

INPUT_GENERATOR_EXIT_CODE=$?

# Kill the OutputMonitor
if kill -0 $OUTPUT_MONITOR_PID 2>/dev/null; then
    echo "Stopping Output Monitor..."
    kill $OUTPUT_MONITOR_PID
fi

# Clean up
if [ "$(docker ps -q -f name=beacon-perf-redis)" ]; then
    echo "Stopping Redis container..."
    docker stop beacon-perf-redis
    docker rm beacon-perf-redis
fi

# Check results
if [ $INPUT_GENERATOR_EXIT_CODE -eq 0 ]; then
    echo "Performance tests completed successfully."
    if [ ! -z "$OUTPUT_FILE" ]; then
        echo "Results saved to $OUTPUT_FILE"
        
        # Run visualization if requested and we have output
        if [ "$VISUALIZE" = "true" ]; then
            echo "Generating visualization reports..."
            mkdir -p "$REPORT_DIR"
            
            # Run the visualization tool
            cd Beacon.PerformanceTester.Visualization
            dotnet run --inputFile="$OUTPUT_FILE" --outputDirectory="$REPORT_DIR"
            VIZ_EXIT_CODE=$?
            
            if [ $VIZ_EXIT_CODE -eq 0 ]; then
                echo "Visualization completed. Reports available in $REPORT_DIR"
            else
                echo "Visualization failed with exit code $VIZ_EXIT_CODE."
            fi
        fi
    fi
else
    echo "Performance tests failed with exit code $INPUT_GENERATOR_EXIT_CODE."
fi

exit $INPUT_GENERATOR_EXIT_CODE