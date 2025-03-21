# End-to-End Guide: From YAML Rules to Running Beacon

This guide provides comprehensive step-by-step instructions for creating custom rules, compiling them with Pulsar using the template-based code generation approach, building the resulting code, and running the AOT-compatible executable with proper configuration. The Pulsar system now uses templates in Pulsar.Compiler/Config/Templates as the source of truth for code generation.

## 1. Creating Custom YAML Rules

First, create a YAML file containing your rule definitions. Rules should follow this structure:

```yaml
rules:
  - name: "SimpleRule"
    description: "Simple temperature threshold rule"
    conditions:
      all:
        - condition:
            type: comparison
            sensor: temperature
            operator: greater_than
            value: 30
    actions:
      - set_value:
          key: temp_alert
          value: 1
```

Save this as `my-rules.yaml`. For more examples, refer to [Rules-Engine.md](Rules-Engine.md) or examine `TestData/sample-rules.yaml`.

## 2. Creating System Configuration

Create a system configuration file (`system_config.yaml`) that includes:

```yaml
version: 1
validSensors:
  - temperature
  - humidity
  - pressure
  - temp_alert
  - system_status
cycleTime: 100  # milliseconds between cycles
redis:
  endpoints:
    - localhost:6379  # Replace with your Redis instance
  poolSize: 4
  retryCount: 3
  retryBaseDelayMs: 100
  connectTimeout: 5000
  syncTimeout: 1000
  keepAlive: 60
  password: null  # Set your Redis password if needed
  ssl: false
  allowAdmin: false
  healthCheck:
    enabled: true
    intervalSeconds: 30
    failureThreshold: 5
    timeoutMs: 2000
  metrics:
    enabled: true
    instanceName: default
    samplingIntervalSeconds: 60
bufferCapacity: 100  # For temporal rules
logLevel: Information  # Optional logging level
logFile: logs/pulsar.log  # Optional log file path
```

## 3. Compiling with Pulsar

### Required Template Files

Pulsar requires specific template files to be present in order to generate code correctly. These templates are searched for in the following locations:

1. `Pulsar.Compiler/Config/Templates/` (direct path from working directory)
2. Relative to the compiler assembly location
3. Relative to the current directory

When using Pulsar from source, these files are automatically available. For compiled or published versions, ensure the templates are copied alongside the executable.

### Template Directory Structure

The required template structure is:

```
Pulsar.Compiler/Config/Templates/
├── Interfaces/
│   ├── ICompiledRules.cs
│   ├── IRedisService.cs
│   ├── IRuleCoordinator.cs
│   └── IRuleGroup.cs
├── Program.cs
├── Project/
│   ├── Generated.sln
│   ├── Runtime.csproj
│   └── trimming.xml
├── Runtime/
│   ├── Buffers/
│   │   ├── CircularBuffer.cs
│   │   ├── IDateTimeProvider.cs
│   │   ├── RingBufferManager.cs
│   │   └── SystemDateTimeProvider.cs
│   ├── Models/
│   │   ├── RedisConfiguration.cs
│   │   └── RuntimeConfig.cs
│   ├── Rules/
│   │   └── RuleBase.cs
│   ├── RuntimeOrchestrator.cs
│   └── Services/
│       ├── RedisConfiguration.cs
│       ├── RedisHealthCheck.cs
│       ├── RedisLoggingConfiguration.cs
│       ├── RedisMetrics.cs
│       ├── RedisMonitoring.cs
│       └── RedisService.cs
└── RuntimeConfig.cs
```

### Compiling Your Rules

Run the Pulsar compiler to generate a Beacon solution:

```bash
dotnet run --project Pulsar.Compiler -- beacon --rules=my-rules.yaml --config=system_config.yaml --output=MyBeacon
```

If using a standalone published version of Pulsar:

```bash
Pulsar.Compiler beacon --rules=my-rules.yaml --config=system_config.yaml --output=MyBeacon
```

This will create a directory structure in `MyBeacon/` containing:
- `Beacon/` - The main solution directory
  - `Beacon.sln` - The solution file
  - `Beacon.Runtime/` - The runtime project
  - `Beacon.Tests/` - Test project

### Version Control Considerations

When working with Pulsar and Beacon in a version-controlled environment, follow these best practices:

1. **Include in Version Control**:
   - Rule definition files (YAML)
   - System configuration files (YAML)
   - Custom scripts for running the compiler
   - Documentation

2. **Exclude from Version Control**:
   - Generated output directories (e.g., `MyBeacon/`)
   - Build artifacts (bin, obj folders)
   - Log files
   
The standard `.gitignore` for Pulsar already excludes these generated directories. If you need to share generated code with others, consider documenting the exact command used to generate it rather than committing the generated files themselves.

## 4. Building the Solution

Build the generated Beacon solution:

```bash
cd MyBeacon/Beacon
dotnet build
```

Or, for an optimized release build:

```bash
dotnet build -c Release
```

## 5. Creating a Standalone Executable (Optional)

To create a standalone executable optimized for your platform:

```bash
# For Windows
dotnet publish -c Release -r win-x64 --self-contained true

# For Linux
dotnet publish -c Release -r linux-x64 --self-contained true

# For macOS
dotnet publish -c Release -r osx-x64 --self-contained true
```

The executable will be generated in:
`Beacon.Runtime/bin/Release/net9.0/<runtime>/publish/`

## 6. Running the Beacon Application

### Basic Execution

To run the compiled Beacon application:

```bash
# For debug build
dotnet run --project Beacon.Runtime

# For published standalone executable
cd Beacon.Runtime/bin/Release/net9.0/<runtime>/publish/
./Beacon.Runtime  # Linux/macOS
Beacon.Runtime.exe  # Windows
```

### Command-Line Arguments

The Beacon application supports these command-line arguments:

```
--config=<path>     Path to a custom runtime configuration JSON file
--redis=<endpoint>  Override the Redis endpoint (format: host:port)
--verbose           Enable verbose logging
--metrics           Enable metrics collection
--interval=<ms>     Override the rule evaluation interval in milliseconds
```

Example:
```bash
./Beacon.Runtime --redis=myredis.example.com:6379 --verbose --interval=200
```

### Runtime Configuration File (Optional)

For advanced settings, create a runtime configuration JSON file:

```json
{
  "redis": {
    "endpoints": ["redis.example.com:6379"],
    "poolSize": 8,
    "retryCount": 3,
    "retryBaseDelayMs": 100,
    "connectTimeout": 5000,
    "syncTimeout": 1000,
    "keepAlive": 60,
    "password": "your-password-here",
    "ssl": true,
    "allowAdmin": false,
    "healthCheck": {
      "enabled": true,
      "intervalSeconds": 30,
      "failureThreshold": 5,
      "timeoutMs": 2000
    },
    "metrics": {
      "enabled": true,
      "instanceName": "production",
      "samplingIntervalSeconds": 60
    }
  },
  "cycleTime": 100,
  "bufferCapacity": 100,
  "logLevel": "Information",
  "logFile": "beacon-logs.txt"
}
```

Run with this configuration:
```bash
./Beacon.Runtime --config=runtime-config.json
```

## 7. Testing Your Rules

### Adding Test Data to Redis

You can use redis-cli to add test data:

```bash
redis-cli SET temperature 25
redis-cli SET humidity 60
```

### Verifying Rule Execution

Check if rules execute correctly by examining the outputs:

```bash
redis-cli GET temp_alert
```

### Monitoring Execution

Enable verbose logging to monitor rule execution:

```bash
./Beacon.Runtime --verbose
```

## 8. Deployment Considerations

### Docker Container Deployment

#### For Beacon Runtime

For containerized deployment of a compiled Beacon application:

```dockerfile
FROM mcr.microsoft.com/dotnet/runtime:9.0
WORKDIR /app
COPY ./Beacon.Runtime/bin/Release/net9.0/linux-x64/publish/ .
ENTRYPOINT ["./Beacon.Runtime", "--redis=redis-host:6379"]
```

Build and run:
```bash
docker build -t beacon-app .
docker run -d --name beacon beacon-app
```

#### For Pulsar Compiler with Templates

For containerized deployment of the Pulsar compiler with all required templates:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /source

# Copy csproj files and restore dependencies
COPY *.sln .
COPY Pulsar.Compiler/*.csproj ./Pulsar.Compiler/
COPY Pulsar.Runtime/*.csproj ./Pulsar.Runtime/
RUN dotnet restore

# Copy source code
COPY Pulsar.Compiler/. ./Pulsar.Compiler/
COPY Pulsar.Runtime/. ./Pulsar.Runtime/

# Publish
RUN dotnet publish -c Release -o /app Pulsar.Compiler/Pulsar.Compiler.csproj

FROM mcr.microsoft.com/dotnet/runtime:9.0
WORKDIR /app
COPY --from=build /app ./
# IMPORTANT: Copy templates to the expected location
COPY --from=build /source/Pulsar.Compiler/Config/Templates ./Pulsar.Compiler/Config/Templates/

# Add sample files
COPY TestData/sample-rules.yaml ./examples/
COPY system_config.yaml ./examples/

ENTRYPOINT ["./Pulsar.Compiler"]
CMD ["--help"]
```

With this Docker image, you can compile rules like this:
```bash
docker run -v $(pwd):/data pulsar-compiler beacon --rules=/data/my-rules.yaml --config=/data/system_config.yaml --output=/data/output
```

### Environment Variables

The runtime also supports environment variables for configuration:

- `BEACON_REDIS_ENDPOINT`: Redis endpoint
- `BEACON_CYCLE_TIME`: Cycle time in milliseconds
- `BEACON_LOG_LEVEL`: Logging level

## 9. Troubleshooting

### Common Issues

1. **Redis Connection Problems**:
   - Verify Redis is running: `redis-cli ping`
   - Check connection settings in config file
   - Ensure network connectivity between Beacon and Redis

2. **Rule Execution Issues**:
   - Enable verbose logging to see rule evaluation
   - Verify sensors exist in Redis with correct format
   - Check for typos in sensor names

3. **Compilation Errors**:
   - Validate YAML syntax
   - Ensure all referenced sensors are in `validSensors`
   - Check for circular dependencies in rules

### Logs

Examine the application logs for detailed error information:

```bash
./Beacon.Runtime --verbose > beacon.log 2>&1
```

## 10. Advanced Topics

### Creating a Self-Contained Pulsar Distribution

To create a self-contained distribution of Pulsar that includes all necessary templates:

```bash
# 1. Build Pulsar.Compiler as a self-contained application
dotnet publish -c Release -r <runtime> --self-contained true Pulsar.Compiler/Pulsar.Compiler.csproj

# 2. Create a distribution folder
mkdir -p PulsarDist

# 3. Copy the compiler and essential files
cp -r Pulsar.Compiler/bin/Release/net9.0/<runtime>/publish/* PulsarDist/
cp -r Pulsar.Compiler/Config/Templates PulsarDist/Pulsar.Compiler/Config/

# Create examples directory
mkdir -p PulsarDist/examples/

# 4. Include example files for reference
cp Examples/BasicRules/temperature_rules.yaml PulsarDist/examples/
cp Examples/BasicRules/system_config.yaml PulsarDist/examples/
cp Examples/BasicRules/reference_config.yaml PulsarDist/examples/
cp Examples/BasicRules/test_run.sh PulsarDist/examples/
cp Examples/README.md PulsarDist/examples/
```

You can now zip this folder and deploy it to any compatible system. The templates will be accessible from the expected relative paths.

### High Availability Setup

For production environments, consider:
- Using Redis Cluster or Redis Sentinel
- Deploying multiple Beacon instances with load balancing
- Setting up monitoring and alerting

See [Redis-Integration.md](Redis-Integration.md) for detailed Redis configuration.

### Performance Tuning

For optimal performance:
- Adjust Redis poolSize based on your workload
- Optimize cycleTime for your specific use case
- Consider adding more instances for high-throughput applications

### Custom Extensions

Consult the testing guide for adding custom functionality to the Beacon runtime.

### Template Customization

Advanced users can customize the templates to:
- Add custom monitoring integration
- Modify the Redis service implementation
- Extend the CircularBuffer implementation
- Add custom metrics collection

To customize templates:
1. Copy the original templates from `Pulsar.Compiler/Config/Templates/`
2. Modify as needed
3. Place them in the same relative path structure
4. Run Pulsar with the `--template-path=<your-templates-dir>` option