using System.Text.Json;
using TodoMeshService.Contracts;
using Orleans;
using ToskaMesh.Runtime;

namespace TodoMeshService.Grains;

public class TodoGrain : Grain, ITodoGrain
{
    private readonly IKeyValueStore _kv;

    public TodoGrain(IKeyValueStore kv)
    {
        _kv = kv;
    }

    public async Task<TodoState?> GetAsync()
    {
        return await _kv.GetAsync<TodoState>(BuildKey());
    }

    public async Task<TodoState> UpsertAsync(string title, bool completed)
    {
        var state = await GetAsync();
        var now = DateTimeOffset.UtcNow;
        state = state is null
            ? new TodoState(this.GetPrimaryKeyString(), title, completed, now)
            : state with { Title = title, Completed = completed };

        await _kv.SetAsync(BuildKey(), state);
        return state;
    }

    public async Task<bool> DeleteAsync()
    {
        return await _kv.DeleteAsync(BuildKey());
    }

    private string BuildKey() => $"todo:{this.GetPrimaryKeyString()}";
}
