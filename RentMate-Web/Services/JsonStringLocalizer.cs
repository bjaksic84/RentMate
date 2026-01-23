using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Localization;
using System.Globalization;
using System.Text.Json;

namespace RentMate.Services
{
    public class JsonStringLocalizer : IStringLocalizer
    {
        private readonly IMemoryCache _cache;
        private readonly string _resourcesPath;

        public JsonStringLocalizer(string resourcesPath, IMemoryCache cache)
        {
            _resourcesPath = resourcesPath;
            _cache = cache;
        }

        public LocalizedString this[string name]
        {
            get
            {
                var val = GetString(name);
                return new LocalizedString(name, val ?? name, val == null);
            }
        }

        public LocalizedString this[string name, params object[] arguments]
        {
            get
            {
                var val = GetString(name);
                var formatted = val == null ? name : string.Format(val, arguments);
                return new LocalizedString(name, formatted, val == null);
            }
        }

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        {
            var culture = CultureInfo.CurrentUICulture.Name;
            var dictionary = GetDictionary(culture);

            return dictionary.Select(kv => new LocalizedString(kv.Key, kv.Value, false));
        }

        private string? GetString(string key)
        {
            var culture = CultureInfo.CurrentUICulture.Name;
            var dictionary = GetDictionary(culture);

            if (dictionary.TryGetValue(key, out var value))
            {
                return value;
            }
            
            // Optional: fallback to parent culture (e.g. sl-SI -> sl)
            // But for now, we assume exact match on file name like "sl.json" or "en.json"

            return null;
        }

        public string GetVersion(string culture)
        {
            var fileName = $"{culture}.json";
            var filePath = Path.Combine(_resourcesPath, fileName);
            if (File.Exists(filePath))
            {
                return File.GetLastWriteTimeUtc(filePath).Ticks.ToString();
            }
            return "0";
        }

        private Dictionary<string, string> GetDictionary(string culture)
        {
            var cacheKey = $"locale_{culture}";
            
            if (!_cache.TryGetValue(cacheKey, out Dictionary<string, string>? dictionary))
            {
                dictionary = new Dictionary<string, string>();
                var fileName = $"{culture}.json";
                var filePath = Path.Combine(_resourcesPath, fileName);

                if (File.Exists(filePath))
                {
                    try
                    {
                        var json = File.ReadAllText(filePath);
                        var values = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                        if (values != null)
                        {
                            dictionary = values;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error loading localization file {filePath}: {ex.Message}");
                    }
                }

                _cache.Set(cacheKey, dictionary, new MemoryCacheEntryOptions
                {
                    SlidingExpiration = TimeSpan.FromHours(1),
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1)
                });
            }

            return dictionary ?? new Dictionary<string, string>();
        }
    }
}
