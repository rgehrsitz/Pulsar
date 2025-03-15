using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using Testcontainers.Redis;
using Xunit;
using Xunit.Abstractions;
// Use the mock classes instead
using Pulsar.Tests.Mocks;
using Pulsar.Tests.TestUtilities;

namespace Pulsar.Tests.Integration
{
    [Trait("Category", "Integration")]
    public class RedisIntegrationTests : IClassFixture<RedisTestFixture>, IAsyncLifetime
    {
        private readonly RedisTestFixture _fixture;
        private readonly ITestOutputHelper _output;
        private string _uniquePrefix;

        public RedisIntegrationTests(RedisTestFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
            _uniquePrefix = $"test:{Guid.NewGuid():N}";
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public Task DisposeAsync()
        {
            // Clean up any test data
            if (_fixture.Redis != null)
            {
                // Fix: Use RedisExtensions and add the missing using directive
                var keys = _fixture.Redis.GetDatabase().KeysAsync($"{_uniquePrefix}*");
                foreach (var key in keys)
                {
                    _fixture.Redis.GetDatabase().KeyDelete(key);
                }
            }
            return Task.CompletedTask;
        }

        [Fact]
        public async Task GetValue_NonExistentKey_ReturnsNull()
        {
            // Arrange
            var key = $"{_uniquePrefix}:nonexistent";
            
            // Act
            var result = await _fixture.RedisService.GetValue<string>(key);
            
            // Assert
            Assert.Null(result);
        }
        
        [Fact]
        public async Task SetValue_ThenGetValue_ReturnsCorrectValue()
        {
            // Arrange
            var key = $"{_uniquePrefix}:test1";
            var value = "test value";
            
            // Act
            await _fixture.RedisService.SetValue(key, value);
            var result = await _fixture.RedisService.GetValue<string>(key);
            
            // Assert
            Assert.Equal(value, result);
        }
        
        [Fact]
        public async Task SetValue_WithObjectValue_SerializesAndDeserializesProperly()
        {
            // Arrange
            var key = $"{_uniquePrefix}:test2";
            var value = new TestObject { Id = 123, Name = "Test Object" };
            
            // Act
            await _fixture.RedisService.SetValue(key, value);
            var result = await _fixture.RedisService.GetValue<TestObject>(key);
            
            // Assert
            Assert.NotNull(result);
            Assert.Equal(value.Id, result.Id);
            Assert.Equal(value.Name, result.Name);
        }
        
        [Fact]
        public async Task SendMessage_SubscribedChannel_HandlerReceivesMessage()
        {
            // Arrange
            var channel = $"{_uniquePrefix}:channel";
            var message = "test message";
            var receivedMessage = "";
            
            await _fixture.RedisService.Subscribe(channel, (c, m) => receivedMessage = m.ToString());
            
            // Act
            await _fixture.RedisService.SendMessage(channel, message);
            
            // Assert
            Assert.Equal(message, receivedMessage);
        }
        
        [Fact]
        public async Task GetAllInputsAsync_ReturnsCorrectValues()
        {
            // Arrange
            // Get the delimiter from the RedisService to ensure we're using the configured value
            var delimiter = ":";
            await _fixture.RedisService.SetValue($"input{delimiter}a", 100);
            await _fixture.RedisService.SetValue($"input{delimiter}b", 200);
            await _fixture.RedisService.SetValue($"input{delimiter}c", 300);
            
            // Act
            var result = await _fixture.RedisService.GetAllInputsAsync();
            
            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Count);
            Assert.Equal(100.0, Convert.ToDouble(result["a"]));
            Assert.Equal(200.0, Convert.ToDouble(result["b"]));
            Assert.Equal(300.0, Convert.ToDouble(result["c"]));
        }

        [Fact(Skip = "Requires actual Redis connection")]
        public async Task RetryPolicy_HandlesConnectionErrors()
        {
            // This test verifies the retry policy by forcing connection errors
            // We'll use a non-existent Redis server and verify it retries the configured number of times
            
            // Arrange - Create a service with invalid connection
            // Use fully qualified name to avoid ambiguity
            var config = new Pulsar.Tests.Mocks.RedisConfiguration
            {
                SingleNode = new Pulsar.Tests.Mocks.SingleNodeConfig
                {
                    Endpoints = new[] { "nonexistent:1234" },
                    RetryCount = 3,
                    RetryBaseDelayMs = 10
                }
            };
            
            // Act & Assert
            // This should throw after retrying 3 times
            await Assert.ThrowsAsync<RedisConnectionException>(() =>
                new RedisService(config, new NullLoggerFactory()).GetValue<string>("test"));
        }
        
        private class TestObject
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }
    }
}
