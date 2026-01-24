using System.Globalization;
using Microsoft.AspNetCore.Http;
using RentMate.Models;

namespace RentMate.Services
{
    public class CurrencyService
    {
        public const string CurrencyCookieName = "RentMateCurrency";
        private readonly IHttpContextAccessor _httpContextAccessor;

        public static readonly List<CurrencyInfo> SupportedCurrencies = new()
        {
            new CurrencyInfo { Code = "EUR", Symbol = "€", Flag = "🇪🇺", Name = "Euro", ExchangeRate = 1.0m },
            new CurrencyInfo { Code = "USD", Symbol = "$", Flag = "🇺🇸", Name = "US Dollar", ExchangeRate = 1.08m },
            new CurrencyInfo { Code = "GBP", Symbol = "£", Flag = "🇬🇧", Name = "British Pound", ExchangeRate = 0.85m },
            new CurrencyInfo { Code = "CHF", Symbol = "CHF", Flag = "🇨🇭", Name = "Swiss Franc", ExchangeRate = 0.96m }
        };

        public CurrencyService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public CurrencyInfo GetCurrentCurrency()
        {
            var cookie = _httpContextAccessor.HttpContext?.Request.Cookies[CurrencyCookieName];
            if (string.IsNullOrEmpty(cookie))
            {
                return SupportedCurrencies[0]; // Default: EUR
            }

            return SupportedCurrencies.FirstOrDefault(c => c.Code == cookie) ?? SupportedCurrencies[0];
        }

        public decimal Convert(decimal? amount)
        {
            if (!amount.HasValue) return 0;
            var currency = GetCurrentCurrency();
            return amount.Value * currency.ExchangeRate;
        }

        public decimal ConvertToBase(decimal? amount)
        {
            if (!amount.HasValue) return 0;
            var currency = GetCurrentCurrency();
            if (currency.ExchangeRate == 0) return amount.Value;
            return amount.Value / currency.ExchangeRate;
        }

        public string Format(decimal? amount, bool includeSymbol = true)
        {
            if (!amount.HasValue) return "";
            
            var currency = GetCurrentCurrency();
            var convertedAmount = amount.Value * currency.ExchangeRate;
            
            // Use current culture for numeric formatting (decimal separator)
            var currentCulture = CultureInfo.CurrentUICulture;
            var formattedNumber = convertedAmount.ToString("N2", currentCulture);

            if (includeSymbol)
            {
                if (currency.Code == "CHF") 
                    return $"{formattedNumber} {currency.Symbol}";
                
                // For Euro, if it's Slovenian culture, symbol usually follows
                if (currency.Code == "EUR" && currentCulture.TwoLetterISOLanguageName == "sl")
                    return $"{formattedNumber} {currency.Symbol}";
                
                return $"{currency.Symbol}{formattedNumber}";
            }

            return formattedNumber;
        }

        public string GetSymbol()
        {
            return GetCurrentCurrency().Symbol;
        }
    }
}
