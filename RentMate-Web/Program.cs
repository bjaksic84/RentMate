using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization; // Dodano za RequestLocalizationOptions
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models; // Dodano za Swagger OpenApi objekti
using RentMate.Data;
using RentMate.Hubs;
using RentMate.Models;
using RentMate.Services;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Localization;
using RentMate.Resources;


// Čiščenje mapiranja claimov, da dobimo čiste "sub", "role" itd.
System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// 1. KONFIGURACIJA STORITEV (SERVICES)
// ==========================================

// --- Baza podatkov ---


var connectionString = builder.Configuration.GetConnectionString("AzureContext");
if (string.IsNullOrEmpty(connectionString))
{
    // To pomaga pri debugiranju migracij
    throw new InvalidOperationException("Connection string 'AzureContext' not found.");
}

builder.Services.AddDbContext<RentMateContext>(options =>
    options.UseSqlServer(connectionString, sqlOptions => 
    {
        // To prepreči napake pri kratkih prekinitvah povezave z Azure strežnikom
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null);
    }));

// --- Identity (Uporabniki in Role) ---
builder.Services.AddDefaultIdentity<ApplicationUser>(options => 
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequireDigit = true; // Primer varnostnih nastavitev
        options.Password.RequiredLength = 6;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<RentMateContext>()
    .AddDefaultTokenProviders();

// Nastavitve piškotkov (za Razor Pages / MVC login)
builder.Services.ConfigureApplicationCookie(options =>
{
    options.AccessDeniedPath = "/AccessDenied";
    options.LoginPath = "/Identity/Account/Login";
    options.SlidingExpiration = true;
});

// --- JWT Avtentikacija (za API) ---
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"] ?? throw new InvalidOperationException("JWT Key is missing in configuration.");
var issuer = jwtSection["Issuer"];
var audience = jwtSection["Audience"];
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

builder.Services.AddAuthentication()
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            
            // Mapiranje claimov
            RoleClaimType = ClaimTypes.Role,
            NameClaimType = ClaimTypes.NameIdentifier
        };
    });

// --- Lokalizacija ---
builder.Services.AddMemoryCache();
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// Register custom JSON localizer factory
builder.Services.AddSingleton<IStringLocalizerFactory, JsonStringLocalizerFactory>();

// --- MVC, Kontrolerji in SignalR ---
builder.Services.AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization(options =>
    {
        options.DataAnnotationLocalizerProvider = (type, factory) =>
            factory.Create(typeof(ValidationMessages));
    })
    .AddJsonOptions(x =>
        x.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles); // Prepreči krožne reference

builder.Services.AddRazorPages();
builder.Services.AddSignalR();

// --- CORS ---
builder.Services.AddCors(options => {
    options.AddPolicy("AllowAll", b => b
        .AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod());
});

// --- Swagger / OpenAPI ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Rešitev za podvojena imena razredov
    options.CustomSchemaIds(type => type.FullName);

    // Konfiguracija za JWT avtentikacijo v Swagger UI
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Vnesite JWT žeton. Primer: eyJhbGciOiJIUzI1NiIs..."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

// --- Lastne storitve ---
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrencyService>();
builder.Services.AddScoped<IFileUploadService, CloudinaryFileUploadService>();

// ==========================================
// 2. BUILD APP
// ==========================================
var app = builder.Build();

// ==========================================
// 3. MIDDLEWARE PIPELINE
// ==========================================

// Seeding podatkov (Admin role itd.)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try 
    {
        var context = services.GetRequiredService<RentMateContext>();
        if (context.Database.GetPendingMigrations().Any())
        {
            await context.Database.MigrateAsync();
        }
        // Dodan try-catch za varnost pri zagonu
        await DataSeeder.SeedRolesAndAdminAsync(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

// Lokalizacija (Mora biti pred Routing in Auth)
var supportedCultures = new[] { new CultureInfo("sl"), new CultureInfo("en") };
var localizationOptions = new RequestLocalizationOptions
{
    DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture("sl"), // Slovenščina kot privzeta
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
};
app.UseRequestLocalization(localizationOptions);

// Error handling in HSTS
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseCors("AllowAll"); // CORS mora biti med Routing in Auth

app.UseAuthentication();
app.UseAuthorization();

// Mapiranje poti
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();
app.MapHub<RentMateHub>("/rentmateHub");

// ==========================================
// 4. RUN
// ==========================================
app.Run();