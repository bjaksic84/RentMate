using RentMate.Models;

namespace RentMate.Services;

/// <summary>
/// Service for currency conversion and formatting.
/// Supports multiple currencies with user preference stored in cookies.
/// </summary>
public interface ICurrencyService
{
    /// <summary>
    /// Gets the list of all supported currencies.
    /// </summary>
    IReadOnlyList<CurrencyInfo> SupportedCurrencies { get; }

    /// <summary>
    /// Gets the user's currently selected currency from cookies.
    /// Defaults to EUR if no preference is set.
    /// </summary>
    CurrencyInfo GetCurrentCurrency();

    /// <summary>
    /// Converts an amount from the base currency (EUR) to the user's selected currency.
    /// </summary>
    /// <param name="amount">The amount in base currency (EUR).</param>
    /// <returns>The converted amount in the user's selected currency.</returns>
    decimal Convert(decimal? amount);

    /// <summary>
    /// Converts an amount from the user's selected currency back to the base currency (EUR).
    /// </summary>
    /// <param name="amount">The amount in the user's selected currency.</param>
    /// <returns>The amount in base currency (EUR).</returns>
    decimal ConvertToBase(decimal? amount);

    /// <summary>
    /// Formats an amount with the user's selected currency symbol.
    /// </summary>
    /// <param name="amount">The amount in base currency (EUR).</param>
    /// <param name="includeSymbol">Whether to include the currency symbol.</param>
    /// <returns>A formatted string with the converted amount and optional symbol.</returns>
    string Format(decimal? amount, bool includeSymbol = true);

    /// <summary>
    /// Gets the symbol for the user's currently selected currency.
    /// </summary>
    string GetSymbol();
}
