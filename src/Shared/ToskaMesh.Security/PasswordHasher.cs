using BCrypt.Net;

namespace ToskaMesh.Security;

/// <summary>
/// Provides password hashing and verification using BCrypt.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Hashes a password using BCrypt.
    /// </summary>
    string HashPassword(string password);

    /// <summary>
    /// Verifies a password against a hash.
    /// </summary>
    bool VerifyPassword(string password, string hash);
}

/// <summary>
/// Implementation of password hashing using BCrypt.
/// </summary>
public class PasswordHasher : IPasswordHasher
{
    private readonly int _workFactor;

    /// <summary>
    /// Initializes a new instance of the PasswordHasher class.
    /// </summary>
    /// <param name="workFactor">BCrypt work factor (default: 12). Higher values increase security but take longer.</param>
    public PasswordHasher(int workFactor = 12)
    {
        if (workFactor < 4 || workFactor > 31)
        {
            throw new ArgumentOutOfRangeException(nameof(workFactor), "Work factor must be between 4 and 31.");
        }

        _workFactor = workFactor;
    }

    /// <inheritdoc/>
    public string HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentNullException(nameof(password), "Password cannot be null or empty.");
        }

        return BCrypt.Net.BCrypt.HashPassword(password, _workFactor);
    }

    /// <inheritdoc/>
    public bool VerifyPassword(string password, string hash)
    {
        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentNullException(nameof(password), "Password cannot be null or empty.");
        }

        if (string.IsNullOrEmpty(hash))
        {
            throw new ArgumentNullException(nameof(hash), "Hash cannot be null or empty.");
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (SaltParseException)
        {
            // Invalid hash format
            return false;
        }
    }
}
