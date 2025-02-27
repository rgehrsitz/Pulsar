# Pulsar Project Guide

## Build & Test Commands
- Build: `dotnet build`
- Run compiler: `dotnet run --project Pulsar.Compiler`
- Run all tests: `dotnet test`
- Run specific test: `dotnet test --filter "FullyQualifiedName=Pulsar.Tests.Integration.IntegrationTests.Integration_EndToEnd_Succeeds"`
- Run tests by category: `dotnet test --filter "Category=Integration"`
- Release build: `dotnet publish -c Release -r win-x64 --self-contained true`

## Code Style Guidelines
- **Naming**: PascalCase for classes/methods/properties, IPrefix for interfaces, _camelCase for private fields
- **Organization**: Namespace matches folder structure, group related functionality in subdirectories
- **Error Handling**: Custom exceptions, proper logging before throws, try/catch with cleanup
- **Dependencies**: Constructor injection, ILogger for logging, dispose resources properly
- **Documentation**: XML comments on public APIs
- **Testing**: xUnit tests with descriptive names (following `ClassName_Scenario_ExpectedResult` pattern)
- **Types**: Use nullable reference types, immutable collections where appropriate
- **Async**: Follow standard async/await patterns with proper cancellation support

*Note: Redis integration tests require Docker with Redis container*