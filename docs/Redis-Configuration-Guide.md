# Redis Configuration Guide

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

## Configuration Options

### Common Options
- `endpoints`: List of Redis server endpoints (host:port)
- `poolSize`: Size of the connection pool (defaults to 2x CPU cores)
- `retryCount`: Number of retry attempts for failed operations
- `retryBaseDelayMs`: Base delay between retries (uses exponential backoff)
- `connectTimeout`: Connection timeout in milliseconds
- `syncTimeout`: Operation timeout in milliseconds
- `keepAlive`: Keep-alive interval in seconds
- `password`: Redis authentication password (null if not required)
- `ssl`: Enable SSL/TLS encryption
- `allowAdmin`: Enable administrative commands

### Health Check Configuration
```json
"healthCheck": {
  "enabled": true,
  "intervalSeconds": 30,
  "failureThreshold": 5,
  "timeoutMs": 2000
}
```

### Metrics Configuration
```json
"metrics": {
  "enabled": true,
  "instanceName": "default",
  "samplingIntervalSeconds": 60
}
```

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
```