using Orleans;

namespace RedisGrainDemo.Contracts;

public interface ICounterGrain : IGrainWithStringKey
{
    Task<int> GetAsync();
    Task<int> IncrementAsync(int delta);
    Task ResetAsync();
}
