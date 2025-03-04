# Debug Workflow for Beacon AOT Implementation

This document explains how to debug the Beacon AOT implementation and test the generated solution.

## Testing the Beacon Implementation

1. Run the generate-beacon.sh script to create a Beacon solution:

```bash
./generate-beacon.sh --rules TestRules.yaml --config system_config.yaml --output TestOutput/beacon-test
```

2. Build the generated solution:

```bash
cd TestOutput/beacon-test/Beacon
dotnet build
```

3. Run the tests:

```bash
dotnet test
```

4. If you want to test the runtime:

```bash
cd Beacon.Runtime/bin/Debug/net9.0
dotnet Beacon.Runtime.dll
```

## Troubleshooting

If the tests fail, check the following:

1. Examine the generated files in `TestOutput/beacon-test/Beacon`
2. Check for errors in the Beacon.Runtime project
3. Check for errors in the RuntimeValidationFixture

## Common Issues

1. **Directory Structure**: Ensure that the directory structure is created correctly
2. **Namespace Conflicts**: Check for namespace conflicts between Pulsar.Runtime and Beacon.Runtime
3. **Redis Dependencies**: Ensure Redis dependencies are properly set up
4. **Temporal Buffer Implementation**: Verify that the CircularBuffer is implemented correctly

## Testing with Docker

For Redis integration tests, you'll need Docker installed. The tests will automatically start a Redis container using TestContainers.

## AOT Compatibility Testing

To verify AOT compatibility:

```bash
cd TestOutput/beacon-test/Beacon
dotnet publish -c Release -r linux-x64 --self-contained true
```

The generated executable should run without any reflection or dynamic code generation.
