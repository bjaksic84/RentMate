namespace RentMate.Models.ViewModels
{
    /// <summary>
    /// ViewModel for the SmartCalendar partial view component.
    /// Supports single date or date range selection with modern .NET DateOnly types.
    /// </summary>
    public class SmartCalendarModel
    {
        /// <summary>Unique identifier for this calendar instance (used for JS initialization)</summary>
        public string Id { get; set; } = "smartCalendar";
        
        /// <summary>Name attribute for the visible input field</summary>
        public string Name { get; set; } = "calendar";
        
        /// <summary>Hidden input name for start date (range mode) or single date</summary>
        public string StartDateField { get; set; } = "startDate";
        
        /// <summary>Hidden input name for end date (range mode only)</summary>
        public string EndDateField { get; set; } = "endDate";
        
        /// <summary>Single date or Range selection mode</summary>
        public CalendarMode Mode { get; set; } = CalendarMode.Range;
        
        /// <summary>Pre-selected start date (optional)</summary>
        public DateOnly? InitialStart { get; set; }
        
        /// <summary>Pre-selected end date (optional, range mode only)</summary>
        public DateOnly? InitialEnd { get; set; }
        
        /// <summary>Minimum selectable date (defaults to today)</summary>
        public DateOnly? MinDate { get; set; }
        
        /// <summary>Maximum selectable date (optional)</summary>
        public DateOnly? MaxDate { get; set; }
        
        /// <summary>Placeholder text for the input field</summary>
        public string? Placeholder { get; set; }
        
        /// <summary>Dates that cannot be selected (e.g., already booked)</summary>
        public IEnumerable<DateOnly>? DisabledDates { get; set; }

        /// <summary>Date ranges that cannot be selected (e.g., existing rental periods)</summary>
        public IEnumerable<DisabledDateRange>? DisabledRanges { get; set; }

        /// <summary>Additional CSS classes for the container</summary>
        public string? CssClass { get; set; }
    }

    public class DisabledDateRange
    {
        public DateOnly From { get; set; }
        public DateOnly To { get; set; }
    }

    public enum CalendarMode
    {
        Single,
        Range
    }
}
