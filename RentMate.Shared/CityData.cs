namespace RentMate.Helpers;

/// <summary>
/// Represents geographic information for a city.
/// </summary>
public class CityInfo
{
    public string Name { get; set; } = string.Empty;
    public double Lat { get; set; }
    public double Lng { get; set; }
}

/// <summary>
/// Static data provider for Slovenian cities with coordinates.
/// Used for location-based features and mapping.
/// </summary>
public static class CityData
{
    #region Constants

    private const double DefaultLatitude = 46.1512;
    private const double DefaultLongitude = 14.9955;
    private const string DefaultCityName = "Slovenija";

    #endregion

    #region City Data

    /// <summary>
    /// List of major Slovenian cities with their coordinates.
    /// Includes district-level entries for larger cities.
    /// </summary>
    public static readonly List<CityInfo> Cities =
    [
        // Ljubljana districts
        new() { Name = "Ljubljana (Center)", Lat = 46.0569, Lng = 14.5058 },
        new() { Name = "Ljubljana (Bežigrad)", Lat = 46.0846, Lng = 14.5123 },
        new() { Name = "Ljubljana (Šiška)", Lat = 46.0798, Lng = 14.4789 },
        new() { Name = "Ljubljana (Vič)", Lat = 46.0374, Lng = 14.4678 },
        new() { Name = "Ljubljana (Moste-Polje)", Lat = 46.0583, Lng = 14.5512 },
        new() { Name = "Ljubljana (Rudnik)", Lat = 46.0279, Lng = 14.5387 },

        // Maribor districts
        new() { Name = "Maribor (Center)", Lat = 46.5547, Lng = 15.6459 },
        new() { Name = "Maribor (Tabor)", Lat = 46.5456, Lng = 15.6433 },
        new() { Name = "Maribor (Tezno)", Lat = 46.5369, Lng = 15.6698 },

        // Other major cities
        new() { Name = "Celje", Lat = 46.2397, Lng = 15.2677 },
        new() { Name = "Kranj", Lat = 46.2389, Lng = 14.3556 },
        new() { Name = "Koper", Lat = 45.5481, Lng = 13.7302 },
        new() { Name = "Novo Mesto", Lat = 45.8011, Lng = 15.1710 },
        new() { Name = "Nova Gorica", Lat = 45.9537, Lng = 13.6484 },
        new() { Name = "Murska Sobota", Lat = 46.6621, Lng = 16.1735 },
        new() { Name = "Velenje", Lat = 46.3636, Lng = 15.1130 },
        new() { Name = "Domžale", Lat = 46.1376, Lng = 14.5936 },
        new() { Name = "Ptuj", Lat = 46.4199, Lng = 15.8696 },
        new() { Name = "Slovenj Gradec", Lat = 46.5103, Lng = 15.0803 },
        new() { Name = "Jesenice", Lat = 46.4367, Lng = 14.0537 },
        new() { Name = "Trbovlje", Lat = 46.1553, Lng = 15.0531 },
        new() { Name = "Kamnik", Lat = 46.2252, Lng = 14.6119 },
        new() { Name = "Izola", Lat = 45.5399, Lng = 13.6594 },
        new() { Name = "Postojna", Lat = 45.7744, Lng = 14.2153 },
        new() { Name = "Kočevje", Lat = 45.6433, Lng = 14.8633 },
        new() { Name = "Logatec", Lat = 45.9161, Lng = 14.2289 },
        new() { Name = "Bled", Lat = 46.3683, Lng = 14.1146 },
        new() { Name = "Portorož", Lat = 45.5147, Lng = 13.5932 }
    ];

    /// <summary>
    /// Alphabetically sorted list of cities for UI display.
    /// </summary>
    public static readonly IReadOnlyList<CityInfo> SortedCities = Cities
        .OrderBy(c => c.Name)
        .ToList();

    #endregion

    #region Public Methods

    /// <summary>
    /// Gets coordinates for a city by name (case-insensitive).
    /// Returns default Slovenia center coordinates if city not found.
    /// </summary>
    /// <param name="cityName">The city name to look up.</param>
    /// <returns>CityInfo with coordinates, or default center of Slovenia.</returns>
    public static CityInfo GetCoordinates(string? cityName)
    {
        if (string.IsNullOrWhiteSpace(cityName))
            return CreateDefaultCity();

        var city = Cities.FirstOrDefault(c => 
            c.Name.Equals(cityName.Trim(), StringComparison.OrdinalIgnoreCase));

        return city ?? CreateDefaultCity();
    }

    #endregion

    #region Private Helpers

    private static CityInfo CreateDefaultCity() => new()
    {
        Name = DefaultCityName,
        Lat = DefaultLatitude,
        Lng = DefaultLongitude
    };

    #endregion
}