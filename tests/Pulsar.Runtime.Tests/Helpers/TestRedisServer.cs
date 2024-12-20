using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace Pulsar.Runtime.Tests.Helpers;

/// <summary>
/// Helper class for testing Redis functionality with an in-memory implementation
/// </summary>
public class TestRedisServer : IDisposable
{
    private readonly Dictionary<string, RedisValue> _data = new();
    private readonly object _lock = new();
    private bool _isConnected = true;
    private bool _isMaster = true;

    public bool IsConnected => _isConnected;
    public bool IsMaster => _isMaster;
    public string Endpoint { get; } = "localhost:6379";

    public void SetConnected(bool connected)
    {
        lock (_lock)
        {
            _isConnected = connected;
        }
    }

    public void SetMaster(bool isMaster)
    {
        lock (_lock)
        {
            _isMaster = isMaster;
        }
    }

    public Task<RedisValue> StringGetAsync(RedisKey key)
    {
        EnsureConnected();
        lock (_lock)
        {
            return Task.FromResult(_data.TryGetValue(key.ToString(), out var value) 
                ? value 
                : RedisValue.Null);
        }
    }

    public Task<RedisValue[]> StringGetAsync(RedisKey[] keys)
    {
        EnsureConnected();
        var results = new RedisValue[keys.Length];
        lock (_lock)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                results[i] = _data.TryGetValue(keys[i].ToString(), out var value)
                    ? value
                    : RedisValue.Null;
            }
        }
        return Task.FromResult(results);
    }

    public Task StringSetAsync(RedisKey key, RedisValue value)
    {
        EnsureConnected();
        lock (_lock)
        {
            _data[key.ToString()] = value;
        }
        return Task.CompletedTask;
    }

    public Task<bool> KeyExistsAsync(RedisKey key)
    {
        EnsureConnected();
        lock (_lock)
        {
            return Task.FromResult(_data.ContainsKey(key.ToString()));
        }
    }

    public IAsyncEnumerable<RedisKey> ScanKeysAsync(RedisValue pattern)
    {
        EnsureConnected();
        return GetMatchingKeysAsync(pattern.ToString());
    }

    private async IAsyncEnumerable<RedisKey> GetMatchingKeysAsync(string pattern)
    {
        pattern = pattern.Replace("*", ".*");
        var regex = new System.Text.RegularExpressions.Regex(pattern);

        lock (_lock)
        {
            foreach (var key in _data.Keys)
            {
                if (regex.IsMatch(key))
                {
                    yield return new RedisKey(key);
                }
            }
        }
    }

    private void EnsureConnected()
    {
        if (!_isConnected)
        {
            throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Test server disconnected");
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _data.Clear();
        }
    }

    public void Dispose()
    {
        Clear();
    }
}
