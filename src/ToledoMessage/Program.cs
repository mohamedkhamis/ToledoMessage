using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using ToledoMessage.Components;
using ToledoMessage.Data;
using ToledoMessage.Models;
using ToledoMessage.Hubs;
using ToledoMessage.Middleware;
using ToledoMessage.Client.Services;
using ToledoMessage.Services;

// ReSharper disable RemoveRedundantBraces

var builder = WebApplication.CreateBuilder(args);

// Serilog structured logging (NFR-011)
builder.Host.UseSerilog(static (context, loggerConfiguration) =>
{
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "ToledoMessage")
        .WriteTo.Console()
        .WriteTo.File("logs/toledomessage-.log", rollingInterval: RollingInterval.Day);
});

// EF Core with SQL Server
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Password hashing (using Identity's hasher without full Identity framework)
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

// Application services
builder.Services.AddScoped<PreKeyService>();
builder.Services.AddScoped<MessageRelayService>();
builder.Services.AddScoped<AccountDeletionService>();
builder.Services.AddHostedService<MessageCleanupHostedService>();
builder.Services.AddHostedService<AccountDeletionHostedService>();
builder.Services.AddSingleton<RateLimitService>();
builder.Services.AddSingleton<PresenceService>();
builder.Services.AddHttpClient("LinkPreview", static client =>
{
    client.Timeout = TimeSpan.FromSeconds(5);
    client.MaxResponseContentBufferSize = 1_048_576;
});
builder.Services.AddScoped<LinkPreviewService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();

// Client services needed during SSR (static server-side rendering)
builder.Services.AddScoped<ToastService>();

// Health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>("sql-server");

// JWT Bearer Authentication
var jwtSection = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSection["SecretKey"]
                ?? throw new InvalidOperationException("Jwt:SecretKey is not configured.");

builder.Services.AddAuthentication(static options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
        };

        // Allow SignalR to receive the token via query string
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = static context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

// SignalR — increase max message size to support encrypted media (up to 16 MB file + Base64 overhead ≈ 30 MB)
builder.Services.AddSignalR(static options =>
{
    options.MaximumReceiveMessageSize = 35 * 1024 * 1024; // 35 MB (16 MB file → ~22 MB ciphertext as Base64 + JSON overhead)
}).AddJsonProtocol(static options =>
{
    // Ensure enum values (ContentType, MessageType) survive positional record deserialization.
    // Without this, enum parameters in records may default to 0 (Text) on some .NET versions.
    options.PayloadSerializerOptions.PropertyNameCaseInsensitive = true;
});

// Controllers (for REST API endpoints)
builder.Services.AddControllers(static options =>
{
    options.Filters.Add<ToledoMessage.Filters.UnauthorizedExceptionFilter>();
});

// Localization
builder.Services.AddLocalization();

// CORS — restrict origins based on environment
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                             ?? [builder.Environment.IsDevelopment() ? "https://localhost:7159" : ""];

        policy.WithOrigins(allowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

// Blazor
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

var app = builder.Build();

// Apply pending EF Core migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

var supportedCultures = new[] { "en", "ar" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("en")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

app.UseRequestLocalization(localizationOptions);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", true);
}

// Fallback for deploy-time files (version.json) not in the static asset manifest.
// MapStaticAssets() only serves build-time fingerprinted assets.
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = static ctx =>
    {
        var path = ctx.Context.Request.Path.Value ?? "";
        if (!path.Contains("version.json", StringComparison.OrdinalIgnoreCase)) return;
        // version.json must never be cached — it's the single trigger for cache busting.
        ctx.Context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
        ctx.Context.Response.Headers.Pragma = "no-cache";
    }
});

// Serilog request logging must be early to capture all requests and errors
app.UseSerilogRequestLogging(static options =>
{
    // Explicit template — avoids accidentally including sensitive URL params, headers, or bodies
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    options.EnrichDiagnosticContext = static (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value ?? "unknown");
        // Intentionally omit Authorization header and request body from diagnostic context
        // to prevent leaking tokens or credentials into structured log sinks (NFR-011)
    };
});

app.UseCors();
app.UseAuthentication();
app.UseMiddleware<RateLimitMiddleware>();
app.UseAuthorization();

// Antiforgery must come after UseAuthentication and UseAuthorization
app.UseAntiforgery();

app.MapStaticAssets();
app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");
app.MapHealthChecks("/health");

app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(ToledoMessage.Client._Imports).Assembly);

app.Run();
