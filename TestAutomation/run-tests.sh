#!/bin/bash

# Run automated tests for Pulsar/Beacon

# Ensure Redis is running
echo "Checking Redis server..."
if ! command -v redis-cli &> /dev/null || ! redis-cli ping &> /dev/null; then
    echo "Redis server not running. Starting Redis with Docker..."
    docker run -d --name pulsar-test-redis -p 6379:6379 redis:latest
    sleep 2
    if ! redis-cli ping &> /dev/null; then
        echo "Failed to start Redis. Please start Redis manually."
        exit 1
    fi
    echo "Redis started successfully."
else
    echo "Redis is already running."
fi

# Clear any existing test data
echo "Clearing existing Redis data..."
redis-cli keys "input:*" | xargs -r redis-cli del
redis-cli keys "output:*" | xargs -r redis-cli del
redis-cli keys "buffer:*" | xargs -r redis-cli del

# Ensure output directories exist
mkdir -p ./BeaconOutput

# Run the test using direct invocation
echo "Running test directly with PulsarBeaconTester..."
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TEST_DIR="$(dirname "$SCRIPT_DIR")"

if ! command -v dotnet &> /dev/null; then
    echo "Error: dotnet command not found. Please install .NET SDK."
    exit 1
fi

# Find the Pulsar.Compiler.dll
COMPILER_DLL=$(find "$TEST_DIR" -name "Pulsar.Compiler.dll" | grep -v "obj" | head -n 1)
if [ -z "$COMPILER_DLL" ]; then
    echo "Building Pulsar.Compiler..."
    if [ -f "$TEST_DIR/Pulsar.sln" ]; then
        dotnet build "$TEST_DIR/Pulsar.sln" -c Debug
        COMPILER_DLL=$(find "$TEST_DIR" -name "Pulsar.Compiler.dll" | grep -v "obj" | head -n 1)
    elif [ -f "$TEST_DIR/Pulsar.Compiler/Pulsar.Compiler.csproj" ]; then
        dotnet build "$TEST_DIR/Pulsar.Compiler/Pulsar.Compiler.csproj" -c Debug
        COMPILER_DLL=$(find "$TEST_DIR" -name "Pulsar.Compiler.dll" | grep -v "obj" | head -n 1)
    fi
fi

if [ -z "$COMPILER_DLL" ]; then
    echo "Error: Could not find or build Pulsar.Compiler.dll"
    exit 1
fi

echo "Found compiler at: $COMPILER_DLL"

# Run manual test using the compiler directly
echo "Compiling rules with Pulsar..."
dotnet "$COMPILER_DLL" beacon --rules="$SCRIPT_DIR/test-rules.yaml" --config="$SCRIPT_DIR/system_config.yaml" --output="$SCRIPT_DIR/BeaconOutput" --target=linux-x64 --verbose

if [ $? -ne 0 ]; then
    echo "Error: Failed to compile rules with Pulsar"
    exit 1
fi

echo "Building Beacon solution..."
cd "$SCRIPT_DIR/BeaconOutput/Beacon"
dotnet build

if [ $? -ne 0 ]; then
    echo "Error: Failed to build Beacon solution"
    exit 1
fi

# Find the Beacon.Runtime.dll
BEACON_DLL=$(find "$SCRIPT_DIR/BeaconOutput" -name "Beacon.Runtime.dll" | grep -v "obj" | head -n 1)
if [ -z "$BEACON_DLL" ]; then
    echo "Error: Could not find Beacon.Runtime.dll after build"
    exit 1
fi

echo "Found Beacon.Runtime at: $BEACON_DLL"
BEACON_DIR=$(dirname "$BEACON_DLL")

# Create appSettings.json for Redis connection
cat > "$BEACON_DIR/appSettings.json" << EOF
{
  "Redis": {
    "Endpoints": [ "localhost:6379" ],
    "PoolSize": 4,
    "RetryCount": 3,
    "RetryBaseDelayMs": 100,
    "ConnectTimeout": 5000,
    "SyncTimeout": 1000,
    "KeepAlive": 60,
    "Password": null
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "BufferCapacity": 100,
  "CycleTimeMs": 100
}
EOF

# Start Beacon in background
echo "Starting Beacon Runtime..."
cd "$BEACON_DIR"
# Check if python-redis is installed
if python3 -c "import redis" 2>/dev/null; then
    echo "Redis Python module is available."
else
    echo "Installing redis Python module..."
    pip3 install redis
fi

dotnet "$BEACON_DLL" --verbose &
BEACON_PID=$!

# Give Redis and Beacon some time to stabilize
echo "Waiting for Beacon to initialize..."
sleep 3

# Check if Beacon process is still running
if ! kill -0 $BEACON_PID 2>/dev/null; then
    echo "Error: Beacon process failed to start or terminated early"
    exit 1
fi

# Run test scenarios
echo "Running test scenarios..."
cd "$SCRIPT_DIR"
PASSED=0
# Parse JSON test scenarios and run each one
python3 -c "
import json, redis, time, sys
r = redis.Redis(host='localhost', port=6379, decode_responses=True)

try:
    # Pre-configure Redis to make sure all keys are cleared
    # and required keys are present with proper format
    for key in r.keys('input:*') + r.keys('output:*') + r.keys('buffer:*'):
        r.delete(key)
    
    # Pre-set empty values for all sensors we want the runtime to recognize
    # Important: Use only hash format for the sensors to avoid WRONGTYPE errors
    timestamp = str(int(time.time()*1000))
    for sensor in ['input:temperature', 'input:humidity', 'input:pressure']:
        r.hset(sensor, mapping={'value': "0", 'timestamp': timestamp})
        
    # Give the Beacon app time to read initial values
    time.sleep(1)
    
    with open('test-scenarios.json') as f:
        scenarios = json.load(f)['scenarios']
    
    all_passed = True
    
    for scenario in scenarios:
        print(f\"Running scenario: {scenario['name']} - {scenario['description']}\")
        
        # Clear existing output data (but keep inputs initialized)
        for key in r.keys('output:*') + r.keys('buffer:*'):
            r.delete(key)
            
        # Set inputs
        if 'inputs' in scenario:
            for key, value in scenario['inputs'].items():
                # ONLY use hash format (what Beacon expects) to avoid WRONGTYPE errors
                timestamp = str(int(time.time()*1000))
                # Delete first to ensure we don't have type conflicts
                r.delete(key)
                r.hset(key, mapping={'value': str(value), 'timestamp': timestamp})
                print(f\"  Set {key} = {value} (timestamp: {timestamp})\")
            
            # Wait for processing
            time.sleep(1)
        
        # Handle input sequences
        if 'inputSequence' in scenario:
            for step_idx, step in enumerate(scenario['inputSequence']):
                step_copy = step.copy()  # Create a copy so we don't modify the original
                delay_ms = 100
                if 'delayMs' in step_copy:
                    delay_ms = step_copy.pop('delayMs')
                
                print(f\"  Step {step_idx+1}:\")
                for key, value in step_copy.items():
                    timestamp = str(int(time.time()*1000))
                    # Delete first to avoid type conflicts
                    r.delete(key)
                    r.hset(key, mapping={'value': str(value), 'timestamp': timestamp})
                    print(f\"    Set {key} = {value}\")
                
                print(f\"    (waiting {delay_ms}ms)\")
                time.sleep(delay_ms / 1000)
            
            # Additional time for processing
            time.sleep(1)
            
        # List all Redis keys for debugging
        print(\"\\nRedis keys after setting inputs:\")
        for key in r.keys('*'):
            print(f\"  {key}\")
        print()
        
        # Check expected outputs
        passed = True
        for key, expected in scenario['expectedOutputs'].items():
            # Try hash format first (Beacon's primary format)
            value = r.hget(key, 'value')
            if value is None:
                # Try string format as fallback
                value = r.get(key)
            
            if value is None:
                print(f\"  ERROR: Output {key} not found\")
                passed = False
                continue
            
            # Compare values with type conversion
            tolerance = scenario.get('tolerance', 0.0001)
            
            if isinstance(expected, bool):
                # Handle boolean comparison
                actual_bool = value.lower() in ('true', '1', 'yes', 't')
                if actual_bool == expected:
                    print(f\"  ✓ {key} = {value} (expected {expected})\")
                else:
                    print(f\"  ✗ {key} = {value} (expected {expected})\")
                    passed = False
            
            elif isinstance(expected, (int, float)):
                # Handle numeric comparison with tolerance
                try:
                    actual_num = float(value)
                    if abs(actual_num - expected) <= tolerance:
                        print(f\"  ✓ {key} = {actual_num} (expected {expected})\")
                    else:
                        print(f\"  ✗ {key} = {actual_num} (expected {expected})\")
                        passed = False
                except ValueError:
                    print(f\"  ✗ {key} = {value} (not a number, expected {expected})\")
                    passed = False
            
            else:
                # String comparison
                if str(value) == str(expected):
                    print(f\"  ✓ {key} = {value} (expected {expected})\")
                else:
                    print(f\"  ✗ {key} = {value} (expected {expected})\")
                    passed = False
        
        if passed:
            print(f\"Scenario PASSED: {scenario['name']}\")
        else:
            print(f\"Scenario FAILED: {scenario['name']}\")
            all_passed = False
        
        print(\"\")
    
    if all_passed:
        print(\"All scenarios PASSED!\")
        sys.exit(0)
    else:
        print(\"Some scenarios FAILED!\")
        sys.exit(1)
        
except Exception as e:
    import traceback
    traceback.print_exc()
    print(f\"Error running scenarios: {e}\")
    sys.exit(2)
" 

PASSED=$?

# Kill the Beacon process
if [ -n "$BEACON_PID" ]; then
    kill $BEACON_PID 2>/dev/null || true
fi

if [ $PASSED -eq 0 ]; then
    echo "All tests PASSED!"
else
    echo "Tests FAILED with exit code $PASSED"
fi

# Return to the original directory
cd "$TEST_DIR"

# Check results
TEST_RESULT=$?

# Clean up
if [ "$(docker ps -q -f name=pulsar-test-redis)" ]; then
    echo "Stopping Redis container..."
    docker stop pulsar-test-redis
    docker rm pulsar-test-redis
fi

if [ $TEST_RESULT -eq 0 ]; then
    echo "All tests PASSED!"
else
    echo "Tests FAILED with exit code $TEST_RESULT"
fi

exit $TEST_RESULT