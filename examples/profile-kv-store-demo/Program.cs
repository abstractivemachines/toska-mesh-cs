using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ToskaMesh.Runtime;

const int DefaultPageSize = 100;
const int MaxPageSize = 500;
const string ProfileKeyPrefix = "profile:";

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

await MeshServiceHost.RunAsync(
    app =>
    {
        app.MapGet("/profiles/{id}", async (string id, IKeyValueStore store, CancellationToken ct) =>
        {
            var profileId = NormalizeId(id);
            if (profileId is null)
            {
                return Results.BadRequest(new { error = "Profile id is required." });
            }

            var profile = await store.GetAsync<ProfileInfo>(BuildKey(profileId), ct);
            return profile is null ? Results.NotFound() : Results.Ok(profile);
        });

        app.MapPut("/profiles/{id}", async (string id, ProfileUpsertRequest request, IKeyValueStore store, CancellationToken ct) =>
        {
            var profileId = NormalizeId(id);
            if (profileId is null)
            {
                return Results.BadRequest(new { error = "Profile id is required." });
            }

            var displayName = NormalizeText(request.DisplayName);
            if (displayName is null)
            {
                return Results.BadRequest(new { error = "DisplayName is required." });
            }

            var email = NormalizeText(request.Email);
            var bio = NormalizeText(request.Bio);
            var tags = NormalizeTags(request.Tags);

            var key = BuildKey(profileId);
            var existing = await store.GetAsync<ProfileInfo>(key, ct);
            var now = DateTimeOffset.UtcNow;

            var profile = existing is null
                ? new ProfileInfo(profileId, displayName, email, bio, tags, now, now)
                : existing with
                {
                    DisplayName = displayName,
                    Email = email,
                    Bio = bio,
                    Tags = tags,
                    UpdatedUtc = now
                };

            await store.SetAsync(key, profile, cancellationToken: ct);

            return existing is null
                ? Results.Created($"/profiles/{profileId}", profile)
                : Results.Ok(profile);
        });

        app.MapDelete("/profiles/{id}", async (string id, IKeyValueStore store, CancellationToken ct) =>
        {
            var profileId = NormalizeId(id);
            if (profileId is null)
            {
                return Results.BadRequest(new { error = "Profile id is required." });
            }

            var deleted = await store.DeleteAsync(BuildKey(profileId), ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        app.MapGet("/profiles", async (int? limit, IKeyValueStore store, CancellationToken ct) =>
        {
            var pageSize = Math.Clamp(limit ?? DefaultPageSize, 1, MaxPageSize);
            var profiles = await store.ListAsync<ProfileInfo>(ProfileKeyPrefix, pageSize, ct);
            return Results.Ok(new ProfileListResponse(profiles.Count, profiles));
        });

        app.MapGet("/health", () => Results.Ok("ok"));
    },
    options =>
    {
        options.ServiceName = "profile-kv-store-demo";
        options.Address = "0.0.0.0";
        options.Port = 8084;
        options.Routing.HealthCheckEndpoint = "/health";
        options.AllowNoopServiceRegistry = true;
        options.RegisterAutomatically = false;
    },
    services =>
    {
        services.AddMeshKeyValueStore(configuration);
    });

static string BuildKey(string id) => $"{ProfileKeyPrefix}{id}";

static string? NormalizeId(string? id)
{
    var trimmed = id?.Trim();
    return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
}

static string? NormalizeText(string? value)
{
    var trimmed = value?.Trim();
    return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
}

static string[] NormalizeTags(string[]? tags)
{
    if (tags is null || tags.Length == 0)
    {
        return Array.Empty<string>();
    }

    return tags
        .Where(tag => !string.IsNullOrWhiteSpace(tag))
        .Select(tag => tag.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}

internal sealed record ProfileUpsertRequest(
    string? DisplayName,
    string? Email,
    string? Bio,
    string[]? Tags);

internal sealed record ProfileInfo(
    string Id,
    string DisplayName,
    string? Email,
    string? Bio,
    string[] Tags,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc);

internal sealed record ProfileListResponse(int Count, IReadOnlyList<ProfileInfo> Profiles);
