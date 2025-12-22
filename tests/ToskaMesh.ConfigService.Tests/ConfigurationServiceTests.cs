using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ToskaMesh.ConfigService.Data;
using ToskaMesh.ConfigService.Entities;
using ToskaMesh.ConfigService.Models;
using ToskaMesh.ConfigService.Services;
using Xunit;

namespace ToskaMesh.ConfigService.Tests;

public class ConfigurationServiceTests
{
    [Fact]
    public async Task UpsertAsync_CreatesNewConfiguration()
    {
        await using var context = CreateDbContext();
        var service = new ConfigurationService(context, new YamlValidationService());

        var result = await service.UpsertAsync(new ConfigurationUpsertRequest(
            "app-settings",
            "production",
            "database:\n  host: localhost",
            "Application settings",
            null
        ), "test-user");

        result.Should().NotBeNull();
        result.Name.Should().Be("app-settings");
        result.Environment.Should().Be("production");
        result.Version.Should().Be("1.0.0");
        result.CreatedBy.Should().Be("test-user");
    }

    [Fact]
    public async Task UpsertAsync_UpdatesExistingConfiguration()
    {
        await using var context = CreateDbContext();
        var service = new ConfigurationService(context, new YamlValidationService());

        // Create initial config
        await service.UpsertAsync(new ConfigurationUpsertRequest(
            "app-settings",
            "dev",
            "key: value1",
            "Initial",
            null
        ), "user1");

        // Update it
        var result = await service.UpsertAsync(new ConfigurationUpsertRequest(
            "app-settings",
            "dev",
            "key: value2",
            "Updated",
            "Changed value"
        ), "user2");

        result.Version.Should().Be("1.0.1");
        result.Content.Should().Be("key: value2");
        result.CreatedBy.Should().Be("user2");
    }

    [Fact]
    public async Task UpsertAsync_SavesVersionHistory()
    {
        await using var context = CreateDbContext();
        var service = new ConfigurationService(context, new YamlValidationService());

        var first = await service.UpsertAsync(new ConfigurationUpsertRequest(
            "my-config",
            "staging",
            "version: 1",
            null,
            null
        ), "admin");

        await service.UpsertAsync(new ConfigurationUpsertRequest(
            "my-config",
            "staging",
            "version: 2",
            null,
            "Upgrade to v2"
        ), "admin");

        var history = await service.GetHistoryAsync(first.Id);

        history.Should().HaveCount(1);
        history.First().Version.Should().Be("1.0.0");
        history.First().Content.Should().Be("version: 1");
    }

    [Fact]
    public async Task UpsertAsync_ThrowsOnInvalidYaml()
    {
        await using var context = CreateDbContext();
        var service = new ConfigurationService(context, new YamlValidationService());

        var act = async () => await service.UpsertAsync(new ConfigurationUpsertRequest(
            "bad-config",
            "dev",
            "this: is: invalid: yaml: {{",
            null,
            null
        ), "user");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Invalid YAML:*");
    }

    [Fact]
    public async Task GetAsync_ReturnsConfiguration()
    {
        await using var context = CreateDbContext();
        var service = new ConfigurationService(context, new YamlValidationService());

        await service.UpsertAsync(new ConfigurationUpsertRequest(
            "logging",
            "production",
            "level: info",
            "Logging config",
            null
        ), "admin");

        var result = await service.GetAsync("logging", "production");

        result.Should().NotBeNull();
        result!.Name.Should().Be("logging");
        result.Content.Should().Be("level: info");
    }

    [Fact]
    public async Task GetAsync_ReturnsNullForNonExistent()
    {
        await using var context = CreateDbContext();
        var service = new ConfigurationService(context, new YamlValidationService());

        var result = await service.GetAsync("nonexistent", "dev");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllConfigurations()
    {
        await using var context = CreateDbContext();
        var service = new ConfigurationService(context, new YamlValidationService());

        await service.UpsertAsync(new ConfigurationUpsertRequest("config-a", "dev", "a: 1", null, null), "user");
        await service.UpsertAsync(new ConfigurationUpsertRequest("config-b", "dev", "b: 2", null, null), "user");
        await service.UpsertAsync(new ConfigurationUpsertRequest("config-c", "prod", "c: 3", null, null), "user");

        var all = await service.GetAllAsync(null);

        all.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetAllAsync_FiltersWithEnvironment()
    {
        await using var context = CreateDbContext();
        var service = new ConfigurationService(context, new YamlValidationService());

        await service.UpsertAsync(new ConfigurationUpsertRequest("config-a", "dev", "a: 1", null, null), "user");
        await service.UpsertAsync(new ConfigurationUpsertRequest("config-b", "prod", "b: 2", null, null), "user");

        var devOnly = await service.GetAllAsync("dev");

        devOnly.Should().HaveCount(1);
        devOnly.First().Name.Should().Be("config-a");
    }

    [Fact]
    public async Task RollbackAsync_RestoresPreviousVersion()
    {
        await using var context = CreateDbContext();
        var service = new ConfigurationService(context, new YamlValidationService());

        var first = await service.UpsertAsync(new ConfigurationUpsertRequest(
            "rollback-test",
            "staging",
            "version: original",
            null,
            null
        ), "admin");

        await service.UpsertAsync(new ConfigurationUpsertRequest(
            "rollback-test",
            "staging",
            "version: updated",
            null,
            null
        ), "admin");

        var success = await service.RollbackAsync(first.Id, new RollbackRequest("1.0.0", "Rolling back"), "admin");

        success.Should().BeTrue();

        var current = await service.GetAsync("rollback-test", "staging");
        current!.Content.Should().Be("version: original");
    }

    [Fact]
    public async Task RollbackAsync_ReturnsFalseForNonExistentConfig()
    {
        await using var context = CreateDbContext();
        var service = new ConfigurationService(context, new YamlValidationService());

        var success = await service.RollbackAsync(Guid.NewGuid(), new RollbackRequest("1.0.0", null), "admin");

        success.Should().BeFalse();
    }

    [Fact]
    public async Task RollbackAsync_ReturnsFalseForNonExistentVersion()
    {
        await using var context = CreateDbContext();
        var service = new ConfigurationService(context, new YamlValidationService());

        var config = await service.UpsertAsync(new ConfigurationUpsertRequest(
            "test",
            "dev",
            "data: test",
            null,
            null
        ), "user");

        var success = await service.RollbackAsync(config.Id, new RollbackRequest("99.99.99", null), "admin");

        success.Should().BeFalse();
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsOrderedByCreatedAt()
    {
        await using var context = CreateDbContext();
        var service = new ConfigurationService(context, new YamlValidationService());

        var config = await service.UpsertAsync(new ConfigurationUpsertRequest("test", "dev", "v1: true", null, null), "user");
        await service.UpsertAsync(new ConfigurationUpsertRequest("test", "dev", "v2: true", null, null), "user");
        await service.UpsertAsync(new ConfigurationUpsertRequest("test", "dev", "v3: true", null, null), "user");

        var history = (await service.GetHistoryAsync(config.Id)).ToList();

        history.Should().HaveCount(2);
        history[0].Version.Should().Be("1.0.1");
        history[1].Version.Should().Be("1.0.0");
    }

    private static ConfigDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ConfigDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ConfigDbContext(options);
    }
}
