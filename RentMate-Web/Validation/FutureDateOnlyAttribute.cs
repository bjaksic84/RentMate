using System.ComponentModel.DataAnnotations;

namespace RentMate.Validation;

/// <summary>
/// Validation attribute that ensures a date is today or in the future.
/// Works with DateTime, DateOnly, and string types.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public class FutureDateOnlyAttribute : ValidationAttribute
{
    #region Constants

    private const string InvalidDateMessage = "The {0} is not a valid date.";
    private const string UnsupportedTypeMessage = "The {0} has an unsupported type for date validation.";

    #endregion

    #region Constructor

    public FutureDateOnlyAttribute()
        : base("The {0} must be today or a future date.")
    {
    }

    #endregion

    #region Validation

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null)
            return ValidationResult.Success;

        var parseResult = TryParseDate(value, validationContext);
        if (parseResult.Error != null)
            return parseResult.Error;

        var today = DateOnly.FromDateTime(DateTime.Today);
        if (parseResult.Date < today)
        {
            return CreateValidationResult(
                FormatErrorMessage(validationContext.DisplayName),
                validationContext.MemberName);
        }

        return ValidationResult.Success;
    }

    #endregion

    #region Private Helpers

    private static (DateOnly Date, ValidationResult? Error) TryParseDate(
        object value,
        ValidationContext context)
    {
        return value switch
        {
            DateOnly dateOnly => (dateOnly, null),
            DateTime dateTime => (DateOnly.FromDateTime(dateTime), null),
            string dateString when DateOnly.TryParse(dateString, out var parsed) => (parsed, null),
            string => (default, CreateValidationResult(
                string.Format(InvalidDateMessage, context.DisplayName),
                context.MemberName)),
            _ => (default, CreateValidationResult(
                string.Format(UnsupportedTypeMessage, context.DisplayName),
                context.MemberName))
        };
    }

    private static ValidationResult CreateValidationResult(string message, string? memberName)
        => new(message, memberName != null ? [memberName] : null);

    #endregion
}
