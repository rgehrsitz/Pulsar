using System;
using System.Collections.Generic;
using System.Linq;
using Serilog;
using StackExchange.Redis;

namespace Pulsar.Runtime.Configuration;

/// <summary>
/// Configures and manages Redis cluster with Sentinel for high availability
/// </summary>
public class RedisClusterConfiguration : IDisposable
{
    private readonly ILogger _logger;
    private readonly string[] _sentinelHosts;
    private readonly string _masterName;
    private readonly string _currentHostname;
    private bool _isPulsarActive;
    private ConnectionMultiplexer? _connection;
    private readonly object _connectionLock = new object();
    private readonly ConfigurationOptions _config;

    public RedisClusterConfiguration(
        ILogger logger,
        string masterName,
        string[] sentinelHosts,
        string currentHostname,
        string? password = null,
        int connectTimeout = 5000,
        int syncTimeout = 1000
    )
    {
        if (string.IsNullOrEmpty(masterName))
            throw new ArgumentException("Master name cannot be empty", nameof(masterName));

        if (string.IsNullOrEmpty(currentHostname))
            throw new ArgumentException(
                "Current hostname cannot be empty",
                nameof(currentHostname)
            );

        if (sentinelHosts == null || sentinelHosts.Length == 0)
            throw new ArgumentException(
                "At least one sentinel host is required",
                nameof(sentinelHosts)
            );

        if (logger == null)
            throw new ArgumentNullException(nameof(logger));

        _logger = logger.ForContext<RedisClusterConfiguration>();
        _masterName = masterName;
        _sentinelHosts = sentinelHosts;
        _currentHostname = currentHostname;

        _config = new ConfigurationOptions
        {
            ServiceName = masterName,     // Used for Sentinel master name
            Password = password,
            ConnectTimeout = connectTimeout,
            SyncTimeout = syncTimeout,
            TieBreaker = "",
            CommandMap = CommandMap.Sentinel,
            DefaultVersion = new Version(3, 0, 0),
            AbortOnConnectFail = false,
            AllowAdmin = true            // Required for Sentinel operations
        };

        foreach (var host in sentinelHosts)
        {
            _config.EndPoints.Add(host);
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
    public virtual ConnectionMultiplexer GetConnection()
    {
        if (_connection?.IsConnected == true)
        {
            return _connection;
        }

        lock (_connectionLock)
        {
            if (_connection?.IsConnected == true)
            {
                return _connection;
            }

            try
            {
                _connection?.Dispose();
                _connection = ConnectionMultiplexer.SentinelConnect(_config);

                if (_connection == null)
                {
                    throw new InvalidOperationException("Failed to create Redis connection");
                }

                _connection.ConnectionFailed += (sender, args) =>
                {
                    _logger.Error(
                        args.Exception,
                        "Redis connection failed to {Endpoint}",
                        args.EndPoint
                    );
                };

                _connection.ConnectionRestored += (sender, args) =>
                {
                    _logger.Information("Redis connection restored to {Endpoint}", args.EndPoint);
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
            var connection = GetConnection();
            var sentinel = connection.GetServer(_sentinelHosts[0]);
            var master = sentinel.SentinelGetMasterAddressByName(_masterName);
            return master?.ToString() ?? throw new InvalidOperationException("No master found");
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
            var connection = GetConnection();
            var endpoints = connection.GetServer(
                connection.GetEndPoints().FirstOrDefault()
                    ?? throw new InvalidOperationException("No Redis endpoints available")
            );

            var replicas = endpoints.SentinelGetReplicaAddresses(_masterName);
            return replicas.Select(r => r?.ToString() ?? string.Empty).Where(r => !string.IsNullOrEmpty(r));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get Redis replicas");
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
