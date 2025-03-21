using StackExchange.Redis;

namespace Pulsar.Tests.TestUtilities
{
    public static class RedisExtensions
    {
        public static IEnumerable<RedisKey> KeysAsync(this IDatabase db, string pattern)
        {
            var server = db.Multiplexer.GetServer(db.Multiplexer.GetEndPoints()[0]);
            return server.Keys(db.Database, pattern);
        }
    }
}
