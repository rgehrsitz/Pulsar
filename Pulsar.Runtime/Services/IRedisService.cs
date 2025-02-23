using System.Collections.Generic;
using System.Threading.Tasks;

namespace Pulsar.Runtime.Services
{
    public interface IRedisService
    {
        Task<Dictionary<string, object>> GetAllInputsAsync();
        Task SetOutputsAsync(Dictionary<string, object> outputs);
        Task<bool> IsHealthyAsync();
    }
}
