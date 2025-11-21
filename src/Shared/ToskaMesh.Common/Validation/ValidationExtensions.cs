using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace ToskaMesh.Common.Validation;

/// <summary>
/// Extension methods for validation operations.
/// </summary>
public static class ValidationExtensions
{
    /// <summary>
    /// Adds FluentValidation validators from the specified assembly.
    /// </summary>
    public static IServiceCollection AddValidators(
        this IServiceCollection services,
        Assembly assembly)
    {
        services.AddValidatorsFromAssembly(assembly);
        return services;
    }

    /// <summary>
    /// Validates an object and throws ValidationException if validation fails.
    /// </summary>
    public static void ValidateAndThrow<T>(this IValidator<T> validator, T instance)
    {
        var result = validator.Validate(instance);
        if (!result.IsValid)
        {
            var errors = result.Errors.Select(e => e.ErrorMessage);
            throw new Middleware.ValidationException("Validation failed", errors);
        }
    }

    /// <summary>
    /// Validates an object asynchronously and throws ValidationException if validation fails.
    /// </summary>
    public static async Task ValidateAndThrowAsync<T>(
        this IValidator<T> validator,
        T instance,
        CancellationToken cancellationToken = default)
    {
        var result = await validator.ValidateAsync(instance, cancellationToken);
        if (!result.IsValid)
        {
            var errors = result.Errors.Select(e => e.ErrorMessage);
            throw new Middleware.ValidationException("Validation failed", errors);
        }
    }

    /// <summary>
    /// Converts FluentValidation results to a dictionary of errors.
    /// </summary>
    public static Dictionary<string, string[]> ToDictionary(this ValidationResult result)
    {
        return result.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray()
            );
    }

    /// <summary>
    /// Gets all error messages as a flat list.
    /// </summary>
    public static IEnumerable<string> GetErrorMessages(this ValidationResult result)
    {
        return result.Errors.Select(e => e.ErrorMessage);
    }
}

/// <summary>
/// Common validation rules for use across the application.
/// </summary>
public static class CommonValidationRules
{
    /// <summary>
    /// Validates that a string is not empty or whitespace.
    /// </summary>
    public static IRuleBuilderOptions<T, string> NotEmptyOrWhitespace<T>(
        this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .NotEmpty()
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("'{PropertyName}' must not be empty or whitespace.");
    }

    /// <summary>
    /// Validates that a string is a valid URL.
    /// </summary>
    public static IRuleBuilderOptions<T, string> MustBeValidUrl<T>(
        this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            .WithMessage("'{PropertyName}' must be a valid HTTP or HTTPS URL.");
    }

    /// <summary>
    /// Validates that a port number is valid (1-65535).
    /// </summary>
    public static IRuleBuilderOptions<T, int> MustBeValidPort<T>(
        this IRuleBuilder<T, int> ruleBuilder)
    {
        return ruleBuilder
            .InclusiveBetween(1, 65535)
            .WithMessage("'{PropertyName}' must be between 1 and 65535.");
    }

    /// <summary>
    /// Validates that a string is a valid IPv4 address.
    /// </summary>
    public static IRuleBuilderOptions<T, string> MustBeValidIPv4<T>(
        this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .Must(ip => System.Net.IPAddress.TryParse(ip, out var address) &&
                       address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            .WithMessage("'{PropertyName}' must be a valid IPv4 address.");
    }
}
