using System;

namespace ToskaMesh.Security;

public static class SecretValidation
{
    private static readonly string[] PlaceholderMarkers =
    {
        "change-me",
        "change-in-production",
        "your-secret",
        "your-secret-key"
    };

    public static void EnsureSecureSecret(string? secret, int minimumLength, string settingName)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException($"{settingName} must be configured and at least {minimumLength} characters.");
        }

        if (secret.Length < minimumLength)
        {
            throw new InvalidOperationException($"{settingName} must be at least {minimumLength} characters. Current length: {secret.Length}.");
        }

        if (LooksLikePlaceholder(secret))
        {
            throw new InvalidOperationException($"{settingName} appears to be a placeholder value. Replace it with a unique secret.");
        }
    }

    public static bool LooksLikePlaceholder(string secret)
    {
        foreach (var marker in PlaceholderMarkers)
        {
            if (secret.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
