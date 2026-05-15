using System.ComponentModel.DataAnnotations;
using RentMate.Infrastructure.Validation;

namespace RentMate.Tests.Infrastructure;

public class ValidationAttributeTests
{
    // ================================================================
    //  DateRangeValidAttribute
    // ================================================================

    private class DateRangeModel
    {
        public DateOnly StartDate { get; set; }

        [DateRangeValid(nameof(StartDate))]
        public DateOnly EndDate { get; set; }
    }

    private static ValidationResult? ValidateDateRange(DateOnly start, DateOnly end)
    {
        var model = new DateRangeModel { StartDate = start, EndDate = end };
        var context = new ValidationContext(model) { MemberName = nameof(DateRangeModel.EndDate) };
        var results = new List<ValidationResult>();
        Validator.TryValidateProperty(model.EndDate, context, results);
        return results.FirstOrDefault();
    }

    [Fact]
    public void DateRange_EndAfterStart_Valid()
    {
        var result = ValidateDateRange(
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 10));
        Assert.Null(result);
    }

    [Fact]
    public void DateRange_EndBeforeStart_Invalid()
    {
        var result = ValidateDateRange(
            new DateOnly(2026, 1, 10),
            new DateOnly(2026, 1, 1));
        Assert.NotNull(result);
    }

    [Fact]
    public void DateRange_EndEqualsStart_Valid()
    {
        var date = new DateOnly(2026, 5, 15);
        var result = ValidateDateRange(date, date);
        Assert.Null(result);
    }

    [Fact]
    public void DateRange_NullEndDate_Valid()
    {
        // Null values should pass (nullable scenarios handled by [Required])
        var attr = new DateRangeValidAttribute("StartDate");
        var model = new { StartDate = new DateOnly(2026, 1, 1) };
        var context = new ValidationContext(model) { MemberName = "EndDate" };
        var result = attr.GetValidationResult(null, context);
        Assert.Equal(ValidationResult.Success, result);
    }

    // ================================================================
    //  FutureDateOnlyAttribute
    // ================================================================

    [Fact]
    public void FutureDate_Tomorrow_Valid()
    {
        var attr = new FutureDateOnlyAttribute();
        var tomorrow = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        var context = new ValidationContext(new object()) { MemberName = "Date" };
        var result = attr.GetValidationResult(tomorrow, context);
        Assert.Equal(ValidationResult.Success, result);
    }

    [Fact]
    public void FutureDate_Today_Valid()
    {
        var attr = new FutureDateOnlyAttribute();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var context = new ValidationContext(new object()) { MemberName = "Date" };
        var result = attr.GetValidationResult(today, context);
        Assert.Equal(ValidationResult.Success, result);
    }

    [Fact]
    public void FutureDate_Yesterday_Invalid()
    {
        var attr = new FutureDateOnlyAttribute();
        var yesterday = DateOnly.FromDateTime(DateTime.Today.AddDays(-1));
        var context = new ValidationContext(new object()) { MemberName = "Date" };
        var result = attr.GetValidationResult(yesterday, context);
        Assert.NotEqual(ValidationResult.Success, result);
    }

    [Fact]
    public void FutureDate_Null_Valid()
    {
        var attr = new FutureDateOnlyAttribute();
        var context = new ValidationContext(new object()) { MemberName = "Date" };
        var result = attr.GetValidationResult(null, context);
        Assert.Equal(ValidationResult.Success, result);
    }
}
