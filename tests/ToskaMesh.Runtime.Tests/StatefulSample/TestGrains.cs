using Orleans;

namespace ToskaMesh.Runtime.Tests.StatefulSample;

public class TestGrain : Grain, ITestGrain
{
    public Task<string> SayHello(string name) => Task.FromResult($"hello {name}");
}
