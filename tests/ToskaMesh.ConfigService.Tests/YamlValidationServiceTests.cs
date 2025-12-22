using FluentAssertions;
using ToskaMesh.ConfigService.Services;
using Xunit;

namespace ToskaMesh.ConfigService.Tests;

public class YamlValidationServiceTests
{
    [Fact]
    public void TryValidate_ReturnsTrueForValidYaml()
    {
        var service = new YamlValidationService();

        var result = service.TryValidate("key: value", out var error);

        result.Should().BeTrue();
        error.Should().BeNull();
    }

    [Fact]
    public void TryValidate_ReturnsTrueForComplexYaml()
    {
        var service = new YamlValidationService();
        var yaml = @"
database:
  host: localhost
  port: 5432
  credentials:
    username: admin
    password: secret
features:
  - name: feature1
    enabled: true
  - name: feature2
    enabled: false
";

        var result = service.TryValidate(yaml, out var error);

        result.Should().BeTrue();
        error.Should().BeNull();
    }

    [Fact]
    public void TryValidate_ReturnsFalseForInvalidYaml()
    {
        var service = new YamlValidationService();

        var result = service.TryValidate("invalid: yaml: syntax: {{", out var error);

        result.Should().BeFalse();
        error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void TryValidate_ReturnsTrueForEmptyString()
    {
        var service = new YamlValidationService();

        var result = service.TryValidate("", out var error);

        result.Should().BeTrue();
        error.Should().BeNull();
    }

    [Fact]
    public void TryValidate_ReturnsTrueForScalarValue()
    {
        var service = new YamlValidationService();

        var result = service.TryValidate("just a string", out var error);

        result.Should().BeTrue();
        error.Should().BeNull();
    }

    [Fact]
    public void TryValidate_HandlesMultipleDocuments()
    {
        var service = new YamlValidationService();
        var yaml = @"
---
doc1: value1
---
doc2: value2
";

        var result = service.TryValidate(yaml, out var error);

        result.Should().BeTrue();
        error.Should().BeNull();
    }

    [Fact]
    public void TryValidate_ReturnsFalseForMalformedIndentation()
    {
        var service = new YamlValidationService();
        var yaml = @"
parent:
  child1: value
 child2: value
";

        var result = service.TryValidate(yaml, out var error);

        result.Should().BeFalse();
        error.Should().NotBeNullOrEmpty();
    }
}
