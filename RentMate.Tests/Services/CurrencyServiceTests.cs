using Microsoft.AspNetCore.Http;
using RentMate.Models.Dto;
using RentMate.Tests.Helpers;

namespace RentMate.Tests.Services;

public class CurrencyServiceTests
{
    private static CurrencyService CreateService(string? cookieValue = null)
    {
        var httpContext = new DefaultHttpContext();
        if (cookieValue != null)
        {
            httpContext.Request.Headers["Cookie"] = $"{CurrencyService.CurrencyCookieName}={cookieValue}";
        }
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(httpContext);
        return new CurrencyService(accessor.Object);
    }

    [Fact]
    public void GetCurrent_NoCookie_ReturnsEUR()
    {
        var sut = CreateService();
        var currency = sut.GetCurrentCurrency();
        Assert.Equal("EUR", currency.Code);
    }

    [Fact]
    public void GetCurrent_ValidCookie_ReturnsCurrency()
    {
        var sut = CreateService("USD");
        var currency = sut.GetCurrentCurrency();
        Assert.Equal("USD", currency.Code);
    }

    [Fact]
    public void GetCurrent_InvalidCookie_FallsBackToEUR()
    {
        var sut = CreateService("INVALID");
        var currency = sut.GetCurrentCurrency();
        Assert.Equal("EUR", currency.Code);
    }

    [Fact]
    public void Convert_EUR_ReturnsUnchanged()
    {
        var sut = CreateService(); // EUR by default
        var result = sut.Convert(100m);
        Assert.Equal(100m, result);
    }

    [Fact]
    public void Convert_USD_AppliesRate()
    {
        var sut = CreateService("USD");
        var result = sut.Convert(100m);
        Assert.Equal(108m, result); // 100 * 1.08
    }

    [Fact]
    public void Convert_NullAmount_ReturnsZero()
    {
        var sut = CreateService("USD");
        var result = sut.Convert(null);
        Assert.Equal(0m, result);
    }

    [Fact]
    public void ConvertToBase_USD_DividesByRate()
    {
        var sut = CreateService("USD");
        var result = sut.ConvertToBase(108m);
        Assert.Equal(100m, result);
    }

    [Fact]
    public void Format_EUR_IncludesSymbol()
    {
        var sut = CreateService();
        var result = sut.Format(50m);
        Assert.Contains("€", result);
    }

    [Fact]
    public void Format_CHF_SymbolAfterNumber()
    {
        var sut = CreateService("CHF");
        var result = sut.Format(50m);
        Assert.Contains("CHF", result);
    }

    [Fact]
    public void GetSymbol_EUR()
    {
        var sut = CreateService();
        Assert.Equal("€", sut.GetSymbol());
    }

    [Fact]
    public void SupportedCurrencies_ReturnsFour()
    {
        var sut = CreateService();
        Assert.Equal(4, sut.SupportedCurrencies.Count);
    }
}
