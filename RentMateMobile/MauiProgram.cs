using Microsoft.Extensions.Logging;
using RentMateMobile.Services; // Prepričaj se, da je pot pravilna
using System.Net.Http.Headers;




namespace RentMateMobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        // 1. Registracija AuthService
        builder.Services.AddSingleton<AuthService>();

        // 2. Registracija Handlerja za JWT
        builder.Services.AddTransient<JwtHttpMessageHandler>();

        // 3. Konfiguracija HttpClienta
        builder.Services.AddHttpClient("RentMateApi", client =>
        {
            // Uporabi port 5276 iz tvojega MauiProgram.cs ali 7000, če imaš HTTPS
            client.BaseAddress = new Uri("http://10.0.2.2:5276/");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            // Ignoriranje SSL napak za lokalni razvoj
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        })
        .AddHttpMessageHandler<JwtHttpMessageHandler>();

        // Registracija HttpClienta za injiciranje v Razor komponente
        builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("RentMateApi"));
        builder.Services.AddScoped<IImageService, ImageService>();

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
