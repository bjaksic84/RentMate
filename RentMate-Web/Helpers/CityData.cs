using System.Globalization;

namespace RentMate.Helpers
{
    public class CityInfo
    {
        public string Name { get; set; } = string.Empty;
        public double Lat { get; set; }
        public double Lng { get; set; }
    }

    public static class CityData
    {
        // Seznam večjih slovenskih mest s koordinatami (Center mesta)
        public static readonly List<CityInfo> Cities = new List<CityInfo>
        {
            // Večja mesta razdeljena na območja
            new CityInfo { Name = "Ljubljana (Center)", Lat = 46.0569, Lng = 14.5058 },
            new CityInfo { Name = "Ljubljana (Bežigrad)", Lat = 46.0846, Lng = 14.5123 },
            new CityInfo { Name = "Ljubljana (Šiška)", Lat = 46.0798, Lng = 14.4789 },
            new CityInfo { Name = "Ljubljana (Vič)", Lat = 46.0374, Lng = 14.4678 },
            new CityInfo { Name = "Ljubljana (Moste-Polje)", Lat = 46.0583, Lng = 14.5512 },
            new CityInfo { Name = "Ljubljana (Rudnik)", Lat = 46.0279, Lng = 14.5387 },

            new CityInfo { Name = "Maribor (Center)", Lat = 46.5547, Lng = 15.6459 },
            new CityInfo { Name = "Maribor (Tabor)", Lat = 46.5456, Lng = 15.6433 },
            new CityInfo { Name = "Maribor (Tezno)", Lat = 46.5369, Lng = 15.6698 },

            // Ostala mesta (Centri)
            new CityInfo { Name = "Celje", Lat = 46.2397, Lng = 15.2677 },
            new CityInfo { Name = "Kranj", Lat = 46.2389, Lng = 14.3556 },
            new CityInfo { Name = "Koper", Lat = 45.5481, Lng = 13.7302 },
            new CityInfo { Name = "Novo Mesto", Lat = 45.8011, Lng = 15.1710 },
            new CityInfo { Name = "Nova Gorica", Lat = 45.9537, Lng = 13.6484 },
            new CityInfo { Name = "Murska Sobota", Lat = 46.6621, Lng = 16.1735 },
            new CityInfo { Name = "Velenje", Lat = 46.3636, Lng = 15.1130 },
            new CityInfo { Name = "Domžale", Lat = 46.1376, Lng = 14.5936 },
            new CityInfo { Name = "Ptuj", Lat = 46.4199, Lng = 15.8696 },
            new CityInfo { Name = "Slovenj Gradec", Lat = 46.5103, Lng = 15.0803 },
            new CityInfo { Name = "Jesenice", Lat = 46.4367, Lng = 14.0537 },
            new CityInfo { Name = "Trbovlje", Lat = 46.1553, Lng = 15.0531 },
            new CityInfo { Name = "Kamnik", Lat = 46.2252, Lng = 14.6119 },
            new CityInfo { Name = "Izola", Lat = 45.5399, Lng = 13.6594 },
            new CityInfo { Name = "Postojna", Lat = 45.7744, Lng = 14.2153 },
            new CityInfo { Name = "Kočevje", Lat = 45.6433, Lng = 14.8633 },
            new CityInfo { Name = "Logatec", Lat = 45.9161, Lng = 14.2289 },
            new CityInfo { Name = "Bled", Lat = 46.3683, Lng = 14.1146 },
            new CityInfo { Name = "Portorož", Lat = 45.5147, Lng = 13.5932 }
        }.OrderBy(c => c.Name).ToList();

        // Metoda za pridobivanje koordinat (Case-insensitive)
        public static CityInfo GetCoordinates(string? cityName)
        {
            if (string.IsNullOrWhiteSpace(cityName))
                return new CityInfo { Name = "Slovenija", Lat = 46.1512, Lng = 14.9955 }; // Default center (Geoss)

            var city = Cities.FirstOrDefault(c => c.Name.Equals(cityName.Trim(), StringComparison.OrdinalIgnoreCase));
            
            // Če mesta ne najdemo na seznamu, vrnemo center Slovenije
            return city ?? new CityInfo { Name = "Slovenija", Lat = 46.1512, Lng = 14.9955 };
        }
    }
}