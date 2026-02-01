using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace RentMate.Services.Localization;

/// <summary>
/// Factory for creating JSON-based string localizers.
/// Ignores the resource source type and returns a global JSON localizer.
/// </summary>
public class JsonStringLocalizerFactory : IStringLocalizerFactory
{
    #region Constants

    private const string DefaultResourcesPath = "Resources";

    #endregion

    #region Fields

    private readonly string _resourcesPath;
    private readonly IWebHostEnvironment _env;
    private readonly IMemoryCache _cache;

    #endregion

    #region Constructor

    public JsonStringLocalizerFactory(
        IOptions<LocalizationOptions> localizationOptions,
        IWebHostEnvironment env,
        IMemoryCache cache)
    {
        _resourcesPath = localizationOptions.Value.ResourcesPath ?? DefaultResourcesPath;
        _env = env;
        _cache = cache;
    }

    #endregion

    #region IStringLocalizerFactory Implementation

    /// <inheritdoc/>
    /// <remarks>
    /// Type is ignored - all localization is flattened to a single JSON source.
    /// </remarks>
    public IStringLocalizer Create(Type resourceSource) => CreateLocalizer();

    /// <inheritdoc/>
    /// <remarks>
    /// Parameters are ignored - all localization is flattened to a single JSON source.
    /// </remarks>
    public IStringLocalizer Create(string baseName, string location) => CreateLocalizer();

    #endregion

    #region Private Helpers

    private IStringLocalizer CreateLocalizer()
    {
        var fullPath = Path.Combine(_env.ContentRootPath, _resourcesPath);
        return new JsonStringLocalizer(fullPath, _cache);
    }

    #endregion
}
