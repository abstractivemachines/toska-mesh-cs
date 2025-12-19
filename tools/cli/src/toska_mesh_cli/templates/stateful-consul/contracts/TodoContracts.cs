using Orleans;

namespace TodoMeshService.Contracts;

public interface ITodoGrain : IGrainWithStringKey
{
    Task<TodoState?> GetAsync();
    Task<TodoState> UpsertAsync(string title, bool completed);
    Task<bool> DeleteAsync();
}

[GenerateSerializer]
public record TodoState(
    [property: Id(0)] string Id,
    [property: Id(1)] string Title,
    [property: Id(2)] bool Completed,
    [property: Id(3)] DateTimeOffset CreatedAt);
