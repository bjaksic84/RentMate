using Microsoft.Extensions.Logging;

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
		// Ustvari HttpClient s podporo za piškotke in ignoriranjem SSL napak za razvoj
		var handler = new HttpClientHandler {
			ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true,
			UseCookies = true,
			CookieContainer = new System.Net.CookieContainer()
		};

		builder.Services.AddSingleton(new HttpClient(handler) { 
			BaseAddress = new Uri("http://10.0.2.2:5276/") 
		});
		builder.Services.AddMauiBlazorWebView();

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
