using System.Globalization;
using Microsoft.AspNetCore.Http;
using RentMate.Models.Domain;
using RentMate.Models.Dto;

using RentMate.Services.Interfaces;

namespace RentMate.Services.Implementations;

/// <summary>
/// Currency conversion and formatting service.
/// Uses cookies to persist user's currency preference.
/// </summary>
public class CurrencyService : ICurrencyService
{
    #region Constants

    /// <summary>
    /// Cookie name for storing the user's currency preference.
    /// </summary>
    public const string CurrencyCookieName = "RentMateCurrency";

    private const string DefaultCurrencyCode = "EUR";

    private static readonly List<CurrencyInfo> Currencies =
    [
        new() { Code = "EUR", Symbol = "€", Flag = "🇪🇺", Name = "Euro", ExchangeRate = 1.0m },
        new() { Code = "USD", Symbol = "$", Flag = "🇺🇸", Name = "US Dollar", ExchangeRate = 1.08m },
        new() { Code = "GBP", Symbol = "£", Flag = "🇬🇧", Name = "British Pound", ExchangeRate = 0.85m },
        new() { Code = "CHF", Symbol = "CHF", Flag = "🇨🇭", Name = "Swiss Franc", ExchangeRate = 0.96m }
    ];

    #endregion

    #region Fields

    private readonly IHttpContextAccessor _httpContextAccessor;

    #endregion

    #region Constructor

    public CurrencyService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    #endregion

    #region Properties

    /// <inheritdoc/>
    public IReadOnlyList<CurrencyInfo> SupportedCurrencies => Currencies;

    #endregion

    #region Public Methods

    /// <inheritdoc/>
    public CurrencyInfo GetCurrentCurrency()
    {
        var cookieValue = _httpContextAccessor.HttpContext?.Request.Cookies[CurrencyCookieName];
        
        return string.IsNullOrEmpty(cookieValue)
            ? GetDefaultCurrency()
            : FindCurrencyByCode(cookieValue) ?? GetDefaultCurrency();
    }

    /// <inheritdoc/>
    public decimal Convert(decimal? amount)
    {
        if (!amount.HasValue) return 0m;
        
        return amount.Value * GetCurrentCurrency().ExchangeRate;
    }

    /// <inheritdoc/>
    public decimal ConvertToBase(decimal? amount)
    {
        if (!amount.HasValue) return 0m;

        var exchangeRate = GetCurrentCurrency().ExchangeRate;
        return exchangeRate == 0m ? amount.Value : amount.Value / exchangeRate;
    }

    /// <inheritdoc/>
    public string Format(decimal? amount, bool includeSymbol = true)
    {
        if (!amount.HasValue) return string.Empty;

        var currency = GetCurrentCurrency();
        var convertedAmount = amount.Value * currency.ExchangeRate;
        var formattedNumber = FormatNumber(convertedAmount);

        return includeSymbol
            ? FormatWithSymbol(formattedNumber, currency)
            : formattedNumber;
    }

    /// <inheritdoc/>
    public string GetSymbol() => GetCurrentCurrency().Symbol;

    #endregion

    #region Private Helpers

    private static CurrencyInfo GetDefaultCurrency() 
        => Currencies.First(c => c.Code == DefaultCurrencyCode);

    private static CurrencyInfo? FindCurrencyByCode(string code) 
        => Currencies.FirstOrDefault(c => c.Code == code);

    private static string FormatNumber(decimal amount) 
        => amount.ToString("N2", CultureInfo.CurrentUICulture);

    /// <summary>
    /// Formats the amount with the currency symbol according to locale conventions.
    /// </summary>
    private static string FormatWithSymbol(string formattedNumber, CurrencyInfo currency)
    {
        var culture = CultureInfo.CurrentUICulture;

        // CHF always appears after the number
        if (currency.Code == "CHF")
            return $"{formattedNumber} {currency.Symbol}";

        // Euro symbol appears after the number in Slovenian locale
        if (currency.Code == "EUR" && culture.TwoLetterISOLanguageName == "sl")
            return $"{formattedNumber} {currency.Symbol}";

        // Default: symbol before the number
        return $"{currency.Symbol}{formattedNumber}";
    }

    #endregion
}

