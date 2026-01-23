namespace RentMate.Models
{
    public class CurrencyInfo
    {
        public string Code { get; set; } = "EUR";
        public string Symbol { get; set; } = "€";
        public string Flag { get; set; } = "🇪🇺";
        public string Name { get; set; } = "Euro";
        public decimal ExchangeRate { get; set; } = 1.0m; // Base is EUR
    }
}
