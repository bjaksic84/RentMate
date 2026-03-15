using System.ComponentModel.DataAnnotations;

namespace RentMate.Infrastructure.Validation;

/// <summary>
/// Validation attribute that requires a boolean property to be true.
/// Used for required consent checkboxes.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public class MustBeTrueAttribute : ValidationAttribute
{
    /// <summary>
    /// Creates a new MustBeTrueAttribute with a default error message.
    /// </summary>
    public MustBeTrueAttribute()
        : base("The {0} field must be accepted.")
    {
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is bool boolValue && boolValue)
            return ValidationResult.Success;

        return new ValidationResult(
            FormatErrorMessage(validationContext.DisplayName),
            [validationContext.MemberName!]);
    }
}
