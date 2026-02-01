namespace RentMate.Models;

/// <summary>
/// Represents currency information for display and conversion.
/// </summary>
public class CurrencyInfo
{
    /// <summary>ISO 4217 currency code (e.g., EUR, USD).</summary>
    public string Code { get; set; } = "EUR";

    /// <summary>Currency symbol (e.g., €, $).</summary>
    public string Symbol { get; set; } = "€";

    /// <summary>Flag emoji for the currency's primary country.</summary>
    public string Flag { get; set; } = "🇪🇺";

    /// <summary>Full currency name.</summary>
    public string Name { get; set; } = "Euro";

    /// <summary>Exchange rate relative to EUR (base currency).</summary>
    public decimal ExchangeRate { get; set; } = 1.0m;
}
