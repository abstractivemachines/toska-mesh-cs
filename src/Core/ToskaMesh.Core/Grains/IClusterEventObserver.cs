using Orleans;
using ToskaMesh.Core.Grains.Models;

namespace ToskaMesh.Core.Grains;

public interface IClusterEventObserver : IGrainObserver
{
    Task OnEventAsync(ClusterEventEnvelope envelope);
}
