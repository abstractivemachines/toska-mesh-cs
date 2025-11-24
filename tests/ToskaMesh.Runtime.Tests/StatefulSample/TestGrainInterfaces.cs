using Orleans;

namespace ToskaMesh.Runtime.Tests.StatefulSample;

public interface ITestGrain : IGrainWithStringKey
{
    Task<string> SayHello(string name);
}
