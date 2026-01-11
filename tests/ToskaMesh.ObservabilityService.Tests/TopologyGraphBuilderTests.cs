using FluentAssertions;
using ToskaMesh.ObservabilityService.Models;
using ToskaMesh.ObservabilityService.Services;
using Xunit;

namespace ToskaMesh.ObservabilityService.Tests;

public class TopologyGraphBuilderTests
{
    [Fact]
    public void Build_ReturnsNodesAndEdges_WithTrafficAndErrorRate()
    {
        var builder = new TopologyGraphBuilder();
        var spans = new List<SpanRecord>
        {
            new("Gateway", "AuthService", 45, false),
            new("Gateway", "AuthService", 80, true),
            new("Gateway", "ConfigService", 30, false),
            new("AuthService", "Postgres", 90, false)
        };

        var graph = builder.Build(spans);

        graph.Nodes.Should().Contain(node => node.Id == "Gateway" && node.Traffic == 3);
        graph.Edges.Should().Contain(edge => edge.From == "Gateway" && edge.To == "AuthService" && edge.Traffic == 2);
        graph.Edges.Should().Contain(edge => edge.From == "AuthService" && edge.To == "Postgres" && edge.Traffic == 1);

        var authEdge = graph.Edges.Single(edge => edge.From == "Gateway" && edge.To == "AuthService");
        authEdge.ErrorRate.Should().BeApproximately(0.5, 0.0001);
    }
}
