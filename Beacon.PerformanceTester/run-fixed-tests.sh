#\!/bin/bash

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

# Ensure scenario file exists
if [ \! -f "$SCENARIO_FILE" ]; then
    echo "Error: Scenario file not found: $SCENARIO_FILE"
    
    # Check if file exists in parent directory
    PARENT_SCENARIO=$(basename "$SCENARIO_FILE")
    if [ -f "$PARENT_SCENARIO" ]; then
        echo "Found scenario file in current directory, copying to InputGenerator directory"
        cp "$PARENT_SCENARIO" "./Beacon.PerformanceTester.InputGenerator/"
        SCENARIO_FILE="./Beacon.PerformanceTester.InputGenerator/$PARENT_SCENARIO"
    else
        exit 1
    fi
fi

# Check if Beacon process is running
if ! pgrep -x "$PROCESS_NAME" > /dev/null; then
    echo "Warning: Beacon process '$PROCESS_NAME' not found. Process monitoring will be disabled."
fi

# Ensure Redis is running
echo "Checking Redis server..."

# Try to ping Redis
if ! redis-cli ping &> /dev/null; then
    echo "Redis server not running. Starting Redis with Docker..."
    # Stop existing container if present
    docker rm -f beacon-perf-redis &> /dev/null || true
    
    # Start a new Redis container
    docker run -d --name beacon-perf-redis -p 6379:6379 redis:latest
    echo "Waiting for Redis to start..."
    sleep 3
    
    # Test if Redis is now running
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

# Create appsettings override files with correct Redis settings
echo "Setting up Redis configuration with connection string: ${REDIS_CONNECTION}"

cat > ./Beacon.PerformanceTester.InputGenerator/bin/Debug/net9.0/appsettings.json << EOL
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "System": "Warning"
    }
  },
  "Redis": {
    "ConnectionString": "${REDIS_CONNECTION}"
  },
  "OutputMonitor": {
    "Host": "localhost",
    "Port": ${MONITOR_PORT}
  }
}
EOL

cat > ./Beacon.PerformanceTester.OutputMonitor/bin/Debug/net9.0/appsettings.json << EOL
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "System": "Warning"
    }
  },
  "Redis": {
    "ConnectionString": "${REDIS_CONNECTION}"
  },
  "Monitor": {
    "Port": ${MONITOR_PORT},
    "ProcessName": "${PROCESS_NAME}"
  }
}
EOL

# Test Redis connection
echo "Testing Redis connection..."
if redis-cli -h $(echo $REDIS_CONNECTION | cut -d ':' -f1) -p $(echo $REDIS_CONNECTION | cut -d ':' -f2) ping > /dev/null; then
  echo "Redis connection test successful."
else
  echo "Redis connection test failed. Check Redis server at ${REDIS_CONNECTION}."
fi

# Prepare arguments for the components
MONITOR_ARGS="--Monitor:Port=${MONITOR_PORT} --Monitor:ProcessName=${PROCESS_NAME}"
GENERATOR_ARGS="--scenarioFile=${SCENARIO_FILE} --OutputMonitor:Port=${MONITOR_PORT}"

if [ \! -z "$DURATION_OVERRIDE" ]; then
    GENERATOR_ARGS="${GENERATOR_ARGS} --DurationOverride=${DURATION_OVERRIDE}"
fi

if [ \! -z "$OUTPUT_FILE" ]; then
    GENERATOR_ARGS="${GENERATOR_ARGS} --outputFile=${OUTPUT_FILE}"
fi

# Start the OutputMonitor in background
echo "Starting Output Monitor..."
cd Beacon.PerformanceTester.OutputMonitor
dotnet run ${MONITOR_ARGS} &
OUTPUT_MONITOR_PID=$\!
cd ..

# Wait briefly for the OutputMonitor to start
sleep 3

# Run the InputGenerator
echo "Running Input Generator..."
cd Beacon.PerformanceTester.InputGenerator
dotnet run ${GENERATOR_ARGS}
INPUT_GENERATOR_EXIT_CODE=$?
cd ..

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
    if [ \! -z "$OUTPUT_FILE" ]; then
        echo "Results saved to $OUTPUT_FILE"
        
        # Run visualization if requested and we have output
        if [ "$VISUALIZE" = "true" ]; then
            echo "Generating visualization reports..."
            mkdir -p "$REPORT_DIR"
            
            # Run the visualization tool
            cd Beacon.PerformanceTester.Visualization
            dotnet run --inputFile="$OUTPUT_FILE" --outputDirectory="$REPORT_DIR"
            VIZ_EXIT_CODE=$?
            cd ..
            
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
