using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Serilog;
using StackExchange.Redis;

namespace Pulsar.Runtime.Configuration;

/// <summary>
/// Configures and manages Redis cluster with Sentinel for high availability
/// </summary>
public class RedisClusterConfiguration
{
    private readonly ILogger _logger;
    private readonly ConfigurationOptions _configurationOptions;
    private readonly string[] _sentinelHosts;
    private readonly string _masterName;
    private readonly string _currentHostname;
    private bool _isPulsarActive;
    private IConnectionMultiplexer? _connection;
    private readonly object _connectionLock = new object();

    public RedisClusterConfiguration(
        ILogger logger,
        string masterName,
        string[] sentinelHosts,
        string currentHostname,
        string? password = null,
        int connectTimeout = 5000,
        int syncTimeout = 5000
    )
    {
        _logger = logger.ForContext<RedisClusterConfiguration>();
        _masterName = masterName;
        _sentinelHosts = sentinelHosts;
        _currentHostname = currentHostname;

        _configurationOptions = new ConfigurationOptions
        {
            CommandMap = CommandMap.Sentinel,
            ServiceName = masterName,
            Password = password,
            ConnectTimeout = connectTimeout,
            SyncTimeout = syncTimeout,
            TieBreaker = "",
            AbortOnConnectFail = false,
        };

        foreach (var host in sentinelHosts)
        {
            _configurationOptions.EndPoints.Add(host);
        }

        _logger.Information(
            "Redis cluster configuration initialized on host {CurrentHost} with {SentinelCount} sentinels for master {MasterName}",
            _currentHostname,
            sentinelHosts.Length,
            masterName
        );
    }

    /// <summary>
    /// Gets a connection to the Redis cluster, creating a new one if necessary
    /// </summary>
    public virtual IConnectionMultiplexer GetConnection()
    {
        if (_connection != null && _connection.IsConnected)
        {
            return _connection;
        }

        lock (_connectionLock)
        {
            if (_connection != null && _connection.IsConnected)
            {
                return _connection;
            }

            try
            {
                _connection?.Dispose();
                _connection = ConnectionMultiplexer.Connect(_configurationOptions);

                // Subscribe to connection events
                _connection.ConnectionFailed += (sender, args) =>
                {
                    _logger.Error(
                        args.Exception,
                        "Redis connection failed to {EndPoint}",
                        args.EndPoint
                    );
                };

                _connection.ConnectionRestored += (sender, args) =>
                {
                    _logger.Information("Redis connection restored to {EndPoint}", args.EndPoint);
                };

                _logger.Information("Successfully connected to Redis cluster");
                return _connection;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to connect to Redis cluster");
                throw;
            }
        }
    }

    /// <summary>
    /// Gets the current Redis master node from Sentinel
    /// </summary>
    public virtual string GetCurrentMaster()
    {
        try
        {
            var sentinel = GetConnection().GetServer(_sentinelHosts[0]);
            var masterInfo = sentinel.SentinelMaster(_masterName);
            if (masterInfo == null)
            {
                throw new InvalidOperationException("Master info is null");
            }
            if (masterInfo == null)
            {
                throw new InvalidOperationException("Master info is null");
            }
            var masterData = JObject.Parse(
                masterInfo?.ToString() ?? throw new InvalidOperationException("Master info is null")
            );
            return $"{masterData["ip"]}:{masterData["port"]}";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get current Redis master");
            throw;
        }
    }

    /// <summary>
    /// Gets all Redis slave nodes from Sentinel
    /// </summary>
    public virtual IEnumerable<string> GetSlaves()
    {
        try
        {
            var sentinel = GetConnection().GetServer(_sentinelHosts[0]);
            var slaves = new List<string>();

            foreach (var slaveInfo in sentinel.SentinelReplicas(_masterName))
            {
                if (slaveInfo == null)
                {
                    throw new InvalidOperationException("Slave info is null");
                }
                var slaveJson = slaveInfo.ToString();
                if (string.IsNullOrEmpty(slaveJson))
                {
                    throw new InvalidOperationException("Slave info JSON is null or empty");
                }
                var slaveData = JObject.Parse(slaveJson);
                slaves.Add($"{slaveData["ip"]}:{slaveData["port"]}");
            }

            return slaves;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get Redis slaves");
            throw;
        }
    }

    /// <summary>
    /// Checks if this Pulsar instance should be active based on Redis master location
    /// </summary>
    public virtual bool ShouldPulsarBeActive()
    {
        try
        {
            var currentMaster = GetCurrentMaster();
            var masterHost = currentMaster.Split(':')[0];
            var shouldBeActive = masterHost.Equals(
                _currentHostname,
                StringComparison.OrdinalIgnoreCase
            );

            if (_isPulsarActive != shouldBeActive)
            {
                _logger.Information(
                    shouldBeActive
                        ? "Activating Pulsar on {Host}"
                        : "Deactivating Pulsar on {Host}",
                    _currentHostname
                );
                _isPulsarActive = shouldBeActive;
            }

            return shouldBeActive;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to determine if Pulsar should be active");
            return false;
        }
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}
