# Redis Integration

## Overview

The Beacon solution uses Redis as its primary data source for both input and output values. The Redis integration is implemented through templates in the Pulsar.Compiler/Config/Templates/Runtime/Services directory, which are included in the generated Beacon application. This document outlines the Redis integration components, their responsibilities, and how they work together to provide a robust and efficient data access layer that is fully AOT-compatible.

## Configuration Types

### Single Node Configuration
- Suitable for development and small-scale deployments
- Uses single Redis instance
- No high availability features

```json
"singleNode": {
  "endpoints": ["localhost:6379"],
  "poolSize": 8,
  "retryCount": 3
  ...
}
```

### Cluster Configuration
- Suitable for production deployments requiring scalability
- Distributes data across multiple nodes
- Supports automatic sharding and replication

```json
"cluster": {
  "endpoints": [
    "redis-node1:6379",
    "redis-node2:6380",
    "redis-node3:6381"
  ],
  "poolSize": 16
  ...
}
```

### High Availability Configuration
- Suitable for production deployments requiring reliability
- Uses Redis master-replica setup
- Automatic failover capabilities

```json
"highAvailability": {
  "endpoints": [
    "redis-master:6379",
    "redis-replica1:6379",
    "redis-replica2:6379"
  ],
  "poolSize": 24
  ...
}
```

## Key Components

### RedisConfiguration

The `RedisConfiguration` class (located in Templates/Runtime/Models) is responsible for managing Redis connection settings and providing configuration options for the Redis service. This class is included in the generated Beacon application.

**Key Features:**
- Support for different deployment types (single node, cluster, high availability)
- Connection endpoint management
- Connection pooling configuration
- Timeout and retry configuration
- Health check configuration
- Metrics collection configuration

**Configuration Options:**
- `endpoints`: List of Redis server endpoints (host:port)
- `poolSize`: Size of the connection pool (defaults to 2x CPU cores)
- `retryCount`: Number of retry attempts for failed operations
- `retryBaseDelayMs`: Base delay between retries (uses exponential backoff)
- `connectTimeout`: Connection timeout in milliseconds
- `syncTimeout`: Operation timeout in milliseconds
- `keepAlive`: Keep-alive interval in seconds
- `ssl`: Enable SSL/TLS encryption
- `password`: Redis authentication password (null if not required)
- `allowAdmin`: Enable administrative commands

### RedisService

The `RedisService` class (located in Templates/Runtime/Services) is the primary interface for interacting with Redis. It implements the `IRedisService` interface (located in Templates/Interfaces) and provides methods for retrieving sensor values and storing output values. This implementation is fully AOT-compatible and included in the generated Beacon application.

**Key Features:**
- Connection management and pooling
- Error handling and retry logic with exponential backoff
- Metrics tracking
- Health monitoring
- Thread-safe operations
- Support for both string and object values

**Primary Methods:**
- `GetValue(string key)`: Get a value from Redis
- `SetValue(string key, object value)`: Set a value in Redis
- `SendMessage(string channel, object message)`: Send a message to a Redis channel
- `Subscribe(string channel, Action<string, object> handler)`: Subscribe to a Redis channel
- `GetAllInputsAsync()`: Get all input values from Redis
- `SetOutputsAsync(Dictionary<string, object> outputs)`: Set multiple output values in Redis

### RedisMetrics

The `RedisMetrics` class tracks various metrics related to Redis operations, such as connection counts, errors, and performance metrics.

**Key Features:**
- Connection count tracking
- Error tracking and categorization
- Retry count tracking
- Performance metrics collection
- Operation count tracking
- Latency tracking

### RedisHealthCheck

The `RedisHealthCheck` class monitors the health of Redis connections and provides health status information.

**Key Features:**
- Connection health monitoring
- Endpoint-specific health tracking
- Health status reporting
- Periodic health checks
- Configurable thresholds for health status

## Integration with RuntimeOrchestrator

The `RuntimeOrchestrator` class uses the `RedisService` to:
1. Retrieve sensor values for rule evaluation
2. Store rule output values
3. Send notifications through Redis channels
4. Monitor Redis health status

The `RuleGroupGeneratorFixed` includes a proper `SendMessage` method implementation that uses the `RedisService` to send messages to Redis channels.

## Error Handling and Resilience

The Redis integration includes several error handling and resilience features:

1. **Connection Pooling**: Efficient management of Redis connections to avoid connection exhaustion
2. **Retry Logic**: Automatic retry of failed operations with exponential backoff
3. **Health Monitoring**: Continuous monitoring of Redis connection health
4. **Failover Support**: Automatic failover for high availability configurations
5. **Error Logging**: Detailed error logging for troubleshooting

## Best Practices

1. **Connection Pool Sizing**
   - For CPU-bound workloads: 2x number of CPU cores
   - For I/O-bound workloads: 4x number of CPU cores
   - Never exceed 50 connections per Redis instance

2. **Retry Strategy**
   - Use exponential backoff (built into configuration)
   - Start with 3-5 retry attempts
   - Set reasonable base delay (100-200ms)

3. **Security**
   - Always enable SSL in production
   - Use strong passwords
   - Restrict allowAdmin to necessary cases only

4. **Monitoring**
   - Enable health checks in production
   - Configure metrics for observability
   - Set appropriate sampling intervals

## Example Configuration

```json
{
  "configurations": {
    "singleNode": {
      "redis": {
        "endpoints": ["localhost:6379"],
        "poolSize": 8,
        "retryCount": 3,
        "retryBaseDelayMs": 100,
        "connectTimeout": 5000,
        "syncTimeout": 1000,
        "keepAlive": 60,
        "password": null,
        "ssl": false,
        "allowAdmin": false
      }
    },
    "cluster": {
      "redis": {
        "endpoints": [
          "redis-node1:6379",
          "redis-node2:6380",
          "redis-node3:6381"
        ],
        "poolSize": 16,
        "retryCount": 3,
        "retryBaseDelayMs": 200,
        "connectTimeout": 5000,
        "syncTimeout": 2000,
        "keepAlive": 60,
        "password": "your-password-here",
        "ssl": true,
        "allowAdmin": true
      }
    },
    "highAvailability": {
      "redis": {
        "endpoints": [
          "redis-master:6379",
          "redis-replica1:6379",
          "redis-replica2:6379"
        ],
        "poolSize": 24,
        "retryCount": 5,
        "retryBaseDelayMs": 100,
        "connectTimeout": 3000,
        "syncTimeout": 1000,
        "keepAlive": 30,
        "password": "your-password-here",
        "ssl": true,
        "allowAdmin": false
      }
    }
  }
}
```

## Example Implementation
```csharp
var config = new RedisConfiguration 
{
    Endpoints = new List<string> { "localhost:6379" },
    PoolSize = Environment.ProcessorCount * 2,
    RetryCount = 3,
    RetryBaseDelayMs = 100,
    ConnectTimeoutMs = 5000,
    KeepAliveSeconds = 60
};

var redis = new RedisService(config);
