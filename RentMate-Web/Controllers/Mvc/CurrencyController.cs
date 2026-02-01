using Microsoft.AspNetCore.Mvc;
using RentMate.Services.Interfaces;
using RentMate.Services.Extensions;
using RentMate.Services.Implementations;

namespace RentMate.Controllers.Mvc;

/// <summary>
/// Controller for managing user currency preferences.
/// </summary>
public class CurrencyController : Controller
{
    private readonly ICurrencyService _currencyService;

    public CurrencyController(ICurrencyService currencyService)
    {
        _currencyService = currencyService;
    }

    [HttpPost]
    public IActionResult SetCurrency(string currency, string returnUrl)
    {
        if (_currencyService.SupportedCurrencies.Any(c => c.Code == currency))
        {
            Response.Cookies.Append(
                CurrencyService.CurrencyCookieName,
                currency,
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) }
            );
        }

        return LocalRedirect(returnUrl);
    }
}

