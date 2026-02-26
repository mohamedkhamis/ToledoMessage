using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ToledoMessage.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddScoped<LocalStorageService>();
builder.Services.AddScoped<AuthTokenHandler>();
builder.Services.AddScoped(sp =>
{
    var handler = sp.GetRequiredService<AuthTokenHandler>();
    handler.InnerHandler = new HttpClientHandler();
    return new HttpClient(handler) { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };
});
builder.Services.AddScoped<KeyGenerationService>();
builder.Services.AddScoped<SessionService>();
builder.Services.AddScoped<CryptoService>();
builder.Services.AddScoped<MessageEncryptionService>();
builder.Services.AddScoped<SignalRService>();
builder.Services.AddScoped<FingerprintService>();
builder.Services.AddScoped<PreKeyReplenishmentService>();
builder.Services.AddScoped<MessageExpiryService>();
builder.Services.AddScoped<ThemeService>();
builder.Services.AddScoped<TabLeaderService>();
builder.Services.AddScoped<NotificationService>();

await builder.Build().RunAsync();
