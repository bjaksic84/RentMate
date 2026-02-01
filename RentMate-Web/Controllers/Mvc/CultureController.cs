using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using RentMate.Resources;
using RentMate.Services.Localization;
using System.Globalization;

namespace RentMate.Controllers.Mvc
{
    public class CultureController : Controller
    {
        private readonly IStringLocalizer _sharedLocalizer;
        private readonly IStringLocalizerFactory _localizerFactory;

        public CultureController(IStringLocalizerFactory factory)
        {
            _localizerFactory = factory;
            _sharedLocalizer = factory.Create(typeof(SharedResources));
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
        /// Includes a version based on file modification time for client-side caching.
        /// </summary>
        [HttpGet]
        [Route("api/translations")]
        public IActionResult GetTranslations(string? v)
        {
            var culture = CultureInfo.CurrentUICulture.Name;
            
            // Get version from the actual localizer
            var localizer = _localizerFactory.Create(typeof(SharedResources)) as JsonStringLocalizer;
            var currentVersion = localizer?.GetVersion(culture) ?? "1";

            // If client version matches, return 304 Not Modified
            if (!string.IsNullOrEmpty(v) && v == currentVersion)
            {
                return StatusCode(304);
            }

            var allStrings = _sharedLocalizer.GetAllStrings(includeParentCultures: true);
            var translations = allStrings.ToDictionary(x => x.Name, x => x.Value);

            return Ok(new 
            { 
                culture, 
                version = currentVersion,
                translations 
            });
        }
    }
}

