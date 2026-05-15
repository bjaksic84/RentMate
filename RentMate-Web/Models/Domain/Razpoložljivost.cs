using RentMate.Services.Implementations;

namespace RentMate.Models.Domain;

/// <summary>
/// Represents an item's availability period.
/// Maps to the VOPC entity Razpoložljivost. Not stored as its own table;
/// availability is computed from existing Rental rows via CalendarService.
/// </summary>
public class Razpoložljivost
{
    public int RazpoložljivostId { get; set; }
    public int PredmetId { get; set; }
    public DateTime DatumOd { get; set; }
    public DateTime DatumDo { get; set; }

    /// <summary>
    /// Checks whether the item is available for the given period.
    /// Maps to VOPC preveriRazpoložljivost(int predmetId, DateTime datumOd, DateTime datumDo).
    /// Delegates to <see cref="ICalendarService"/> for the actual lookup so the
    /// entity stays the design's answer to availability questions.
    /// </summary>
    public static async Task<bool> PreveriRazpoložljivostAsync(
        ICalendarService calendarService,
        int predmetId,
        DateTime datumOd,
        DateTime datumDo,
        CancellationToken ct = default)
    {
        return await calendarService.IsDateRangeAvailableAsync(
            predmetId,
            DateOnly.FromDateTime(datumOd),
            DateOnly.FromDateTime(datumDo),
            cancellationToken: ct);
    }
}
