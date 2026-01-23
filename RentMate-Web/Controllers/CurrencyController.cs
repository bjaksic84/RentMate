using Microsoft.AspNetCore.Mvc;
using RentMate.Services;

namespace RentMate.Controllers
{
    public class CurrencyController : Controller
    {
        [HttpPost]
        public IActionResult SetCurrency(string currency, string returnUrl)
        {
            if (CurrencyService.SupportedCurrencies.Any(c => c.Code == currency))
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
}
