using Orleans;
using Orleans.Runtime;
using RedisGrainDemo.Contracts;

namespace RedisGrainDemo.Silo.Grains;

[GenerateSerializer]
public sealed class CounterState
{
    [Id(0)] public int Value { get; set; }
    [Id(1)] public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
}

public class CounterGrain : Grain, ICounterGrain
{
    private readonly IPersistentState<CounterState> state;

    public CounterGrain([PersistentState("counter", "Default")] IPersistentState<CounterState> state)
    {
        this.state = state;
    }

    public Task<int> GetAsync() => Task.FromResult(state.State.Value);

    public async Task<int> IncrementAsync(int delta)
    {
        state.State.Value += delta;
        state.State.LastUpdatedUtc = DateTime.UtcNow;
        await state.WriteStateAsync();
        return state.State.Value;
    }

    public async Task ResetAsync()
    {
        state.State.Value = 0;
        state.State.LastUpdatedUtc = DateTime.UtcNow;
        await state.WriteStateAsync();
    }
}
