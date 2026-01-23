using Microsoft.Extensions.Localization;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;

namespace RentMate.Services
{
    public class JsonStringLocalizer : IStringLocalizer
    {
        private readonly ConcurrentDictionary<string, Dictionary<string, string>> _cache = new();
        private readonly string _resourcesPath;

        public JsonStringLocalizer(string resourcesPath)
        {
            _resourcesPath = resourcesPath;
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

        private Dictionary<string, string> GetDictionary(string culture)
        {
            return _cache.GetOrAdd(culture, c =>
            {
                var dictionary = new Dictionary<string, string>();
                var fileName = $"{c}.json";
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

                return dictionary;
            });
        }
    }
}
