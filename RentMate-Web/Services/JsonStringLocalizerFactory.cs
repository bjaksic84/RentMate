using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace RentMate.Services
{
    public class JsonStringLocalizerFactory : IStringLocalizerFactory
    {
        private readonly string _resourcesPath;
        private readonly IWebHostEnvironment _env;
        private readonly IMemoryCache _cache;

        public JsonStringLocalizerFactory(IOptions<LocalizationOptions> localizationOptions, IWebHostEnvironment env, IMemoryCache cache)
        {
            _resourcesPath = localizationOptions.Value.ResourcesPath ?? "Resources";
            _env = env;
            _cache = cache;
        }

        public IStringLocalizer Create(Type resourceSource)
        {
            // We ignore the type and always return the global JSON localizer
            // This effectively flattens all localization to a single source
            return CreateLocalizer();
        }

        public IStringLocalizer Create(string baseName, string location)
        {
            return CreateLocalizer();
        }

        private IStringLocalizer CreateLocalizer()
        {
            var fullPath = Path.Combine(_env.ContentRootPath, _resourcesPath);
            return new JsonStringLocalizer(fullPath, _cache);
        }
    }
}
