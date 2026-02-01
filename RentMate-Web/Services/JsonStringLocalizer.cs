using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Localization;
using System.Globalization;
using System.Text.Json;

namespace RentMate.Services;

/// <summary>
/// JSON-based string localizer that loads translations from JSON files.
/// Supports caching for performance and culture-specific resources.
/// </summary>
public class JsonStringLocalizer : IStringLocalizer
{
    #region Constants

    private const string CacheKeyPrefix = "locale_";
    private static readonly TimeSpan SlidingExpiration = TimeSpan.FromHours(1);
    private static readonly TimeSpan AbsoluteExpiration = TimeSpan.FromDays(1);
    private const string DefaultVersion = "0";

    #endregion

    #region Fields

    private readonly IMemoryCache _cache;
    private readonly string _resourcesPath;

    #endregion

    #region Constructor

    public JsonStringLocalizer(string resourcesPath, IMemoryCache cache)
    {
        _resourcesPath = resourcesPath;
        _cache = cache;
    }

    #endregion

    #region IStringLocalizer Implementation

    /// <inheritdoc/>
    public LocalizedString this[string name]
    {
        get
        {
            var value = GetString(name);
            return new LocalizedString(name, value ?? name, resourceNotFound: value == null);
        }
    }

    /// <inheritdoc/>
    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            var value = GetString(name);
            var formatted = value == null ? name : string.Format(value, arguments);
            return new LocalizedString(name, formatted, resourceNotFound: value == null);
        }
    }

    /// <inheritdoc/>
    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
    {
        var culture = CultureInfo.CurrentUICulture.Name;
        var dictionary = GetDictionary(culture);

        return dictionary.Select(kv => new LocalizedString(kv.Key, kv.Value, resourceNotFound: false));
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Gets a version identifier for the specified culture's resource file.
    /// Useful for cache invalidation.
    /// </summary>
    public string GetVersion(string culture)
    {
        var filePath = GetResourceFilePath(culture);
        return File.Exists(filePath) 
            ? File.GetLastWriteTimeUtc(filePath).Ticks.ToString() 
            : DefaultVersion;
    }

    #endregion

    #region Private Helpers

    private string? GetString(string key)
    {
        var culture = CultureInfo.CurrentUICulture.Name;
        var dictionary = GetDictionary(culture);

        return dictionary.TryGetValue(key, out var value) ? value : null;
    }

    private Dictionary<string, string> GetDictionary(string culture)
    {
        var cacheKey = $"{CacheKeyPrefix}{culture}";

        if (_cache.TryGetValue(cacheKey, out Dictionary<string, string>? dictionary) && dictionary != null)
            return dictionary;

        dictionary = LoadDictionaryFromFile(culture);
        CacheDictionary(cacheKey, dictionary);

        return dictionary;
    }

    private Dictionary<string, string> LoadDictionaryFromFile(string culture)
    {
        var filePath = GetResourceFilePath(culture);

        if (!File.Exists(filePath))
            return new Dictionary<string, string>();

        try
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) 
                   ?? new Dictionary<string, string>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading localization file {filePath}: {ex.Message}");
            return new Dictionary<string, string>();
        }
    }

    private string GetResourceFilePath(string culture) 
        => Path.Combine(_resourcesPath, $"{culture}.json");

    private void CacheDictionary(string cacheKey, Dictionary<string, string> dictionary)
    {
        var cacheOptions = new MemoryCacheEntryOptions
        {
            SlidingExpiration = SlidingExpiration,
            AbsoluteExpirationRelativeToNow = AbsoluteExpiration
        };

        _cache.Set(cacheKey, dictionary, cacheOptions);
    }

    #endregion
}
