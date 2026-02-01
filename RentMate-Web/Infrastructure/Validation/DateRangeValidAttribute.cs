using System.ComponentModel.DataAnnotations;

namespace RentMate.Infrastructure.Validation;

/// <summary>
/// Validation attribute that ensures a date is after or equal to another date property.
/// Useful for validating end dates in date ranges.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public class DateRangeValidAttribute : ValidationAttribute
{
    #region Fields

    private readonly string _comparisonProperty;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new DateRangeValidAttribute.
    /// </summary>
    /// <param name="comparisonProperty">The name of the property to compare against (typically the start date).</param>
    public DateRangeValidAttribute(string comparisonProperty)
        : base("The {0} must be on or after the {1}.")
    {
        _comparisonProperty = comparisonProperty;
    }

    #endregion

    #region Validation

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null)
            return ValidationResult.Success;

        var comparisonValue = GetComparisonValue(validationContext);
        if (comparisonValue == null)
            return ValidationResult.Success;

        var endDate = ParseToDateOnly(value);
        var startDate = ParseToDateOnly(comparisonValue);

        if (endDate < startDate)
        {
            var errorMessage = string.Format(
                ErrorMessageString,
                validationContext.DisplayName,
                _comparisonProperty);

            return new ValidationResult(errorMessage, [validationContext.MemberName!]);
        }

        return ValidationResult.Success;
    }

    #endregion

    #region Private Helpers

    private object? GetComparisonValue(ValidationContext context)
    {
        var propertyInfo = context.ObjectType.GetProperty(_comparisonProperty);
        return propertyInfo?.GetValue(context.ObjectInstance);
    }

    private static DateOnly ParseToDateOnly(object value) => value switch
    {
        DateOnly dateOnly => dateOnly,
        DateTime dateTime => DateOnly.FromDateTime(dateTime),
        string dateString when DateOnly.TryParse(dateString, out var parsed) => parsed,
        _ => throw new InvalidOperationException($"Cannot parse {value.GetType().Name} to DateOnly")
    };

    #endregion
}
