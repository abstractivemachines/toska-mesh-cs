using Orleans;

namespace TodoMeshService.Contracts;

public interface ITodoGrain : IGrainWithStringKey
{
    Task<TodoState?> GetAsync();
    Task<TodoState> UpsertAsync(string title, bool completed);
    Task<bool> DeleteAsync();
}

public record TodoState(string Id, string Title, bool Completed, DateTimeOffset CreatedAt);
