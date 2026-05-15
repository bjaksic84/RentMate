using System.ComponentModel.DataAnnotations;

namespace RentMate.Shared.Contracts.Validation;

/// <summary>
/// Validates that a URL is either null/empty or points to a trusted Cloudinary domain.
/// Prevents arbitrary external URLs from being stored as image references.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public class CloudinaryUrlAttribute : ValidationAttribute
{
    private const string CloudinaryDomain = "res.cloudinary.com";

    public CloudinaryUrlAttribute()
        : base("Image URL must be a valid Cloudinary URL.")
    {
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not string url || string.IsNullOrWhiteSpace(url))
            return ValidationResult.Success; // Null/empty is allowed (optional field)

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && uri.Scheme == "https"
            && uri.Host.Equals(CloudinaryDomain, StringComparison.OrdinalIgnoreCase))
        {
            return ValidationResult.Success;
        }

        return new ValidationResult(
            FormatErrorMessage(validationContext.DisplayName),
            [validationContext.MemberName!]);
    }
}
