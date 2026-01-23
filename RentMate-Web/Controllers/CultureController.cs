using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using RentMate.Resources;
using System.Globalization;

namespace RentMate.Controllers
{
    public class CultureController : Controller
    {
        private readonly IStringLocalizer<SharedResources> _sharedLocalizer;

        public CultureController(IStringLocalizer<SharedResources> sharedLocalizer)
        {
            _sharedLocalizer = sharedLocalizer;
        }

        [HttpPost]
        public IActionResult SetLanguage(string culture, string returnUrl)
        {
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) }
            );

            return LocalRedirect(returnUrl);
        }

        /// <summary>
        /// Returns all shared translations for the current culture as JSON.
        /// Used by JavaScript to access localized strings on the client side.
        /// </summary>
        [HttpGet]
        [Route("api/translations")]
        public IActionResult GetTranslations()
        {
            var culture = CultureInfo.CurrentUICulture.Name;
            var allStrings = _sharedLocalizer.GetAllStrings(includeParentCultures: true);
            var translations = allStrings.ToDictionary(x => x.Name, x => x.Value);

            return Ok(new 
            { 
                culture, 
                translations 
            });
        }
    }
}
