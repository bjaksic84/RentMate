using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.Localization;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using RentMate.Hubs;
using RentMate.Resources;
using RentMate.Infrastructure.Data;
using RentMate.Infrastructure.Middleware;
using RentMate.Models.Domain;
using RentMate.Services.Interfaces;
using RentMate.Services.Implementations;
using RentMate.Services.Localization;


// Clear default claim type mappings to use standard "sub", "role" etc.
System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// 1. SERVICE CONFIGURATION
// ==========================================

// --- Database Configuration ---


var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found in configuration.");

builder.Services.AddDbContext<RentMateContext>(options =>
    options.UseNpgsql(connectionString, sqlOptions => 
    {
        // To prepreči napake pri kratkih prekinitvah povezave z Azure strežnikom
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorCodesToAdd: null);
    }));

// --- Identity (Users and Roles) ---
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;

        if (builder.Environment.IsDevelopment())
        {
            // Relaxed password policy for easier testing
            options.Password.RequireDigit = false;
            options.Password.RequiredLength = 4;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireLowercase = false;
            options.Password.RequiredUniqueChars = 1;

            // Relaxed lockout — more attempts, shorter lockout
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromSeconds(30);
            options.Lockout.MaxFailedAccessAttempts = 50;
            options.Lockout.AllowedForNewUsers = true;
        }
        else
        {
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
        }

        // User settings
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<RentMateContext>()
    .AddDefaultTokenProviders();

// Cookie settings for Razor Pages / MVC login
builder.Services.ConfigureApplicationCookie(options =>
{
    options.AccessDeniedPath = "/AccessDenied";
    options.LoginPath = "/Identity/Account/Login";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(24);
    
    // Security cookie settings
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() 
        ? CookieSecurePolicy.SameAsRequest 
        : CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.Name = "RentMate.Auth";
});

// --- JWT Authentication (for API) ---
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"] 
    ?? throw new InvalidOperationException("JWT Key is missing in configuration.");
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
            
            // Claim type mapping
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

// --- Localization ---
builder.Services.AddMemoryCache();
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// Register custom JSON localizer factory
builder.Services.AddSingleton<IStringLocalizerFactory, JsonStringLocalizerFactory>();

// --- MVC, Controllers, and SignalR ---
builder.Services.AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization(options =>
    {
        options.DataAnnotationLocalizerProvider = (type, factory) =>
            factory.Create(typeof(ValidationMessages));
    })
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);

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
        opt.PermitLimit = builder.Environment.IsDevelopment() ? 100 : 5;
        opt.Window = builder.Environment.IsDevelopment() ? TimeSpan.FromMinutes(1) : TimeSpan.FromMinutes(5);
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
    
    // Development policy - ONLY available in DEBUG builds AND Development environment
    // This double-guard prevents accidental permissive CORS in production
    #if DEBUG
    if (builder.Environment.IsDevelopment())
    {
        options.AddPolicy("Development", b => b
            .SetIsOriginAllowed(_ => true) // Allow any origin in dev
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
    }
    #endif
});

// --- Swagger / OpenAPI ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Fix for duplicate class names
    options.CustomSchemaIds(type => type.FullName);

    // JWT authentication configuration for Swagger UI
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

// --- Custom Services ---
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrencyService, CurrencyService>();
builder.Services.AddScoped<IFileUploadService, CloudinaryFileUploadService>();
builder.Services.AddScoped<IReviewAggregationService, ReviewAggregationService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<ICalendarService, CalendarService>();
builder.Services.AddScoped<IPaymentService, StripePaymentService>();
builder.Services.AddScoped<IDepositService, DepositService>();
builder.Services.AddScoped<IRentalExtensionService, RentalExtensionService>();
builder.Services.AddScoped<IAccessoryService, AccessoryService>();
builder.Services.AddHostedService<OverdueRentalService>();
builder.Services.AddHostedService<DataRetentionService>();

// --- Marketplace Ranking System ---
builder.Services.AddScoped<IScoringService, ScoringService>();
builder.Services.AddHostedService<ScoringBackgroundService>();

// --- Onboarding & Profile Completion ---
builder.Services.AddScoped<IProfileCompletionService, ProfileCompletionService>();

// --- Account Lifecycle (deactivation, reactivation, GDPR deletion) ---
builder.Services.AddScoped<IAccountLifecycleService, AccountLifecycleService>();

// ==========================================
// 2. BUILD APP
// ==========================================
var app = builder.Build();

// ==========================================
// 3. MIDDLEWARE PIPELINE
// ==========================================

// Database seeding (Admin roles, etc.)
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
        
        await DataSeeder.SeedRolesAndAdminAsync(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

// Localization (must be before Routing and Auth)
var supportedCultures = new[] { new CultureInfo("sl"), new CultureInfo("en") };
var localizationOptions = new RequestLocalizationOptions
{
    DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture("sl"), // Slovenian as default
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
};
app.UseRequestLocalization(localizationOptions);

// Error handling and HSTS
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    #if DEBUG
    app.UseCors("Development");
    #else
    app.UseCors("SecurePolicy"); // Fallback if somehow in Dev without DEBUG
    #endif
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseCors("SecurePolicy");
}

// Security Headers
app.UseSecurityHeaders();

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseResponseCompression();

app.UseRouting();

// Rate Limiting (must be after routing, before auth)
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// Block deactivated users from all routes (MVC, Razor Pages, API)
app.UseDeactivatedAccountCheck();

// Route mapping
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();
app.MapHub<RentMateHub>("/rentmateHub");

// ==========================================
// 4. RUN
// ==========================================
app.Run();