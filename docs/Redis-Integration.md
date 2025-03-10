# Redis Integration in Beacon

## Overview

The Beacon solution uses Redis as its primary data source for both input and output values. This document outlines the Redis integration components, their responsibilities, and how they work together to provide a robust and efficient data access layer.

## Key Components

### RedisConfiguration

The `RedisConfiguration` class is responsible for managing Redis connection settings and providing configuration options for the Redis service.

**Key Features:**
- Support for different deployment types (single node, cluster, high availability)
- Connection endpoint management
- Connection pooling configuration
- Timeout and retry configuration
- Health check configuration
- Metrics collection configuration

**Configuration Options:**
- `endpoints`: List of Redis server endpoints (host:port)
- `poolSize`: Size of the connection pool
- `retryCount`: Number of retry attempts for failed operations
- `retryBaseDelayMs`: Base delay between retries with exponential backoff
- `connectTimeout`: Connection timeout in milliseconds
- `syncTimeout`: Operation timeout in milliseconds
- `keepAlive`: Keep-alive interval in seconds
- `ssl`: Enable SSL/TLS encryption
- `password`: Redis authentication password

### RedisService

The `RedisService` class is the primary interface for interacting with Redis. It implements the `IRedisService` interface and provides methods for retrieving sensor values and storing output values.

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

## Configuration Options

Different Redis deployment types are supported through configuration:

### Single Node Configuration
```json
"singleNode": {
  "endpoints": ["localhost:6379"],
  "poolSize": 8,
  "retryCount": 3
  ...
}
```

### Cluster Configuration
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
```json
"highAvailability": {
  "endpoints": [
    "redis-master:6379",
    "redis-replica1:6379",
    "redis-replica2:6379"
  ],
  "poolSize": 16,
  "replicaOnly": false
  ...
}
```

## Best Practices

1. **Connection Pooling**: Configure an appropriate pool size based on your workload
2. **Timeouts**: Set appropriate timeouts for your environment
3. **Retry Strategy**: Configure retry count and delay based on your environment
4. **Health Checks**: Enable health checks to monitor Redis connection health
5. **Metrics**: Enable metrics collection for monitoring and troubleshooting

## Conclusion

The Redis integration in Beacon provides a robust and efficient data access layer for rule evaluation. The implementation includes connection pooling, error handling, health monitoring, and support for various deployment configurations, making it suitable for production deployments.
