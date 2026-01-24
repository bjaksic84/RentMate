using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using RentMate.Data;
using RentMate.Hubs;
using RentMate.Models;
using RentMate.Services;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
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
        
        // Strong password policy
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequiredUniqueChars = 4;
        
        // Lockout settings (protection against brute force)
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;
        
        // User settings
        options.User.RequireUniqueEmail = true;
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
    options.ExpireTimeSpan = TimeSpan.FromHours(24);
    
    // Security cookie settings
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.Name = "RentMate.Auth";
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
        options.RequireHttpsMetadata = true; // Always require HTTPS for JWT
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
            ClockSkew = TimeSpan.FromMinutes(1), // Allow 1 minute tolerance for clock differences
            
            // Mapiranje claimov
            RoleClaimType = ClaimTypes.Role,
            NameClaimType = ClaimTypes.NameIdentifier
        };
        
        // Log token validation failures for security monitoring
        options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogWarning("JWT Authentication failed: {Message}", context.Exception.Message);
                return Task.CompletedTask;
            }
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

// Performance: Response Compression
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

// --- Anti-Forgery Configuration ---
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN"; // For AJAX requests
    options.Cookie.Name = "RentMate.Antiforgery";
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() 
        ? CookieSecurePolicy.SameAsRequest 
        : CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

// --- Rate Limiting ---
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    
    // Global rate limit
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User.Identity?.Name ?? context.Request.Headers.Host.ToString(),
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));
    
    // Strict policy for authentication endpoints
    options.AddFixedWindowLimiter("AuthPolicy", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(5);
        opt.QueueLimit = 0;
    });
    
    // API policy
    options.AddFixedWindowLimiter("ApiPolicy", opt =>
    {
        opt.PermitLimit = 60;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 2;
    });
});

// --- CORS (Restrict to specific origins in production) ---
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() 
    ?? new[] { 
        "https://localhost:7000",
        "https://localhost:5001",
        "http://localhost:5000",
        "https://rentmate-gdc6decvaqapckcx.polandcentral-01.azurewebsites.net",
        "https://www.rentmate.com",
        "https://rentmate.com"
    };

builder.Services.AddCors(options => {
    options.AddPolicy("SecurePolicy", b => b
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
    
    // Development policy (only in dev environment)
    if (builder.Environment.IsDevelopment())
    {
        options.AddPolicy("Development", b => b
            .SetIsOriginAllowed(_ => true) // Allow any origin in dev
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
    }
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
    app.UseCors("Development");
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseCors("SecurePolicy");
}

// Security Headers Middleware
app.Use(async (context, next) =>
{
    // Remove server identification headers
    context.Response.Headers.Remove("Server");
    context.Response.Headers.Remove("X-Powered-By");
    context.Response.Headers.Remove("X-AspNet-Version");
    
    // Prevent clickjacking
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    
    // Prevent MIME type sniffing
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    
    // Enable XSS filtering
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    
    // Referrer Policy
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    
    // Content Security Policy (adjust as needed for your CDN/external resources)
    context.Response.Headers.Append("Content-Security-Policy", 
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com; " +
        "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com https://fonts.googleapis.com; " +
        "font-src 'self' https://cdn.jsdelivr.net https://fonts.gstatic.com; " +
        "img-src 'self' data: https: blob:; " +
        "connect-src 'self' https: wss:; " +
        "frame-ancestors 'none';");
    
    // Permissions Policy
    context.Response.Headers.Append("Permissions-Policy", 
        "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()");
    
    await next();
});

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseResponseCompression();

app.UseRouting();

// Rate Limiting (must be after routing, before auth)
app.UseRateLimiter();

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