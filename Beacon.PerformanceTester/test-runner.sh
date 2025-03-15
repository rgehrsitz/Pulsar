#\!/bin/bash

# Simple test runner for Beacon Performance Tester
# This script focuses on the essentials to make testing work

# Default values
SCENARIO_FILE="./beacon-aligned-test.json"
OUTPUT_FILE="results.json"
REPORT_DIR="./reports"

# Kill any running processes from previous runs
echo "Cleaning up previous runs..."
pkill -f "Beacon.PerformanceTester.OutputMonitor" || true
pkill -f "Beacon.PerformanceTester.InputGenerator" || true
sleep 1

# Choose a random port for the TCP communication
export MONITOR_PORT=$((5000 + RANDOM % 1000))
echo "Using TCP port: $MONITOR_PORT"

# Initialize Redis
echo "Starting Redis with Docker..."
docker rm -f beacon-perf-redis &> /dev/null || true
docker run -d --name beacon-perf-redis -p 6379:6379 redis:latest
echo "Waiting for Redis to start..."
sleep 2

# Ensure Redis is running before proceeding
for i in {1..5}; do
    if redis-cli ping | grep -q PONG; then
        echo "Redis is running."
        break
    fi
    if [ $i -eq 5 ]; then
        echo "Redis failed to start. Please check Docker."
        exit 1
    fi
    echo "Waiting for Redis (attempt $i)..."
    sleep 2
done

# Build the projects
echo "Building projects..."
dotnet build

# Clear any existing data
echo "Flushing all Redis data..."
redis-cli flushall
redis-cli flushdb

# Create output directory
mkdir -p "$REPORT_DIR"

# Create output directory
mkdir -p "$REPORT_DIR"

# Copy test scenario file to output directory if needed
if [ \! -f "./Beacon.PerformanceTester.InputGenerator/bin/Debug/net9.0/$SCENARIO_FILE" ]; then
    cp "$SCENARIO_FILE" "./Beacon.PerformanceTester.InputGenerator/bin/Debug/net9.0/" || true
fi

# Check for Beacon process
PROCESS_NAME="dotnet"
PROCESS_ID=$(pgrep -f "$PROCESS_NAME" | head -1 || echo "")
if [ -n "$PROCESS_ID" ]; then
    echo "Using process $PROCESS_NAME (PID: $PROCESS_ID) for monitoring"
else
    echo "No $PROCESS_NAME process found, will use placeholder"
    PROCESS_ID="1"  # Use PID 1 as placeholder
fi

# Start output monitor in background
echo "Starting Output Monitor..."
(cd ./Beacon.PerformanceTester.OutputMonitor/bin/Debug/net9.0/ && dotnet Beacon.PerformanceTester.OutputMonitor.dll --Redis:ConnectionString="localhost:6379" --Monitor:Port=${MONITOR_PORT} --Monitor:ProcessName="$PROCESS_NAME") &
MONITOR_PID=$\!

# Wait for monitor to initialize
sleep 3

# Run input generator
echo "Running Input Generator..."
(cd ./Beacon.PerformanceTester.InputGenerator/bin/Debug/net9.0/ && dotnet Beacon.PerformanceTester.InputGenerator.dll --Redis:ConnectionString="localhost:6379" --scenarioFile="$SCENARIO_FILE" --OutputMonitor:Port=${MONITOR_PORT} --outputFile="$OUTPUT_FILE")
GENERATOR_EXIT=$?

# Stop output monitor
if kill -0 $MONITOR_PID 2>/dev/null; then
    echo "Stopping Output Monitor..."
    kill $MONITOR_PID
fi

# Check if visualization is needed
if [ $GENERATOR_EXIT -eq 0 ] && [ -f "$OUTPUT_FILE" ]; then
    echo "Test successful. Results saved to $OUTPUT_FILE"
    
    # Run visualization
    echo "Generating visualization reports..."
    (cd ./Beacon.PerformanceTester.Visualization/bin/Debug/net9.0/ && dotnet Beacon.PerformanceTester.Visualization.dll --inputFile="../../../../../$OUTPUT_FILE" --outputDirectory="../../../../../$REPORT_DIR")
    
    if [ $? -eq 0 ]; then
        echo "Visualization completed. Reports available in $REPORT_DIR"
    else
        echo "Visualization failed."
    fi
else
    echo "Test failed with exit code $GENERATOR_EXIT"
fi

# Clean up Redis container
echo "Cleaning up Redis container..."
docker stop beacon-perf-redis
docker rm beacon-perf-redis

exit $GENERATOR_EXIT
