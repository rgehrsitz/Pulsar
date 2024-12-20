# Redis Cluster Deployment Scripts

This directory contains scripts for deploying the Redis cluster across two buildings with failover capabilities.

## Configuration

1. Edit `../config/redis/deployment-config.json`:
   - Update IP addresses and hostnames
   - Set secure Redis password
   - Adjust paths if needed

## Deployment Options

### Option 1: Remote Deployment (PowerShell)

Uses PowerShell remoting to deploy to all servers:

```powershell
.\Deploy-RedisCluster.ps1 -ConfigPath "..\config\redis\deployment-config.json"
```

Requirements:
- PowerShell remoting enabled on all servers
- Administrative credentials for all servers
- Network connectivity between all nodes

### Option 2: Local Deployment (Batch)

Run on each server individually:

```batch
deploy-local.bat
```

Requirements:
- Administrative privileges on local machine
- Redis binaries installed in C:\Redis

## Deployment Process

1. Creates required directories
2. Copies configuration files
3. Installs Redis and/or Sentinel services
4. Starts services

## Verification

After deployment:

1. Check services are running:
```powershell
Get-Service Redis, RedisSentinel
```

2. Test Redis connectivity:
```
redis-cli -h localhost -p 6379 ping
```

3. Check Sentinel status:
```
redis-cli -p 26379 sentinel master pulsar-master
```

## Troubleshooting

1. Check logs in `C:\Redis\logs`
2. Verify firewall rules for Redis (6379) and Sentinel (26379) ports
3. Ensure all nodes can resolve hostnames

## Notes

- Redis runs only on server1a and server2a
- Sentinel runs on all nodes
- Pulsar should be deployed only on Redis servers
- Automatic failover occurs if master fails
