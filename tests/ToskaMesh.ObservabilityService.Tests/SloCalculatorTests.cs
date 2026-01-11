using FluentAssertions;
using ToskaMesh.ObservabilityService.Models;
using ToskaMesh.ObservabilityService.Services;
using Xunit;

namespace ToskaMesh.ObservabilityService.Tests;

public class SloCalculatorTests
{
    [Fact]
    public void Evaluate_ReturnsOk_WhenBurnRateBelowThreshold()
    {
        var calculator = new SloCalculator();
        var definition = new SloDefinition(
            "gateway-availability",
            "Gateway",
            "Gateway availability",
            "Keep success rate high.",
            0.99,
            TimeSpan.FromDays(30),
            2.0);
        var windows = new List<SloWindowData>
        {
            new("gateway-availability", "1h", TimeSpan.FromHours(1), 100_000, 800, DateTimeOffset.UtcNow)
        };

        var status = calculator.Evaluate(definition, windows);

        status.IsBurnRateAlert.Should().BeFalse();
        status.Severity.Should().Be("ok");
        status.MaxBurnRate.Should().BeLessThan(2.0);
    }

    [Fact]
    public void Evaluate_ReturnsAlert_WhenBurnRateExceedsThreshold()
    {
        var calculator = new SloCalculator();
        var definition = new SloDefinition(
            "auth-availability",
            "AuthService",
            "Auth availability",
            "Keep auth success rate high.",
            0.99,
            TimeSpan.FromDays(30),
            2.0);
        var windows = new List<SloWindowData>
        {
            new("auth-availability", "1h", TimeSpan.FromHours(1), 50_000, 1_500, DateTimeOffset.UtcNow)
        };

        var status = calculator.Evaluate(definition, windows);

        status.IsBurnRateAlert.Should().BeTrue();
        status.Severity.Should().Be("warning");
        status.MaxBurnRate.Should().BeGreaterThan(2.0);
    }
}
