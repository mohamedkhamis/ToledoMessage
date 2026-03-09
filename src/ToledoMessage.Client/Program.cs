using System.Text.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using ToledoMessage.Client.Services;
using ToledoMessage.Shared.Converters;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddLocalization();
// Register JSON options with LongToStringConverter for HttpClient deserialization
builder.Services.AddSingleton(static _ =>
{
    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    options.Converters.Add(new LongToStringConverter());
    options.Converters.Add(new LongNullableToStringConverter());
    return options;
});
builder.Services.AddScoped<LocalStorageService>();
builder.Services.AddScoped<AuthTokenHandler>();
builder.Services.AddScoped(sp =>
{
    var handler = sp.GetRequiredService<AuthTokenHandler>();
    handler.InnerHandler = new HttpClientHandler();
    return new HttpClient(handler) { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };
});
builder.Services.AddScoped<KeyGenerationService>();
builder.Services.AddScoped<KeyBackupCryptoService>();
builder.Services.AddScoped<KeyBackupService>();
builder.Services.AddScoped<SessionService>();
builder.Services.AddScoped<CryptoService>();
builder.Services.AddScoped<MessageEncryptionService>();
builder.Services.AddScoped<SignalRService>();
builder.Services.AddScoped<FingerprintService>();
builder.Services.AddScoped<PreKeyReplenishmentService>();
builder.Services.AddScoped<MessageExpiryService>();
builder.Services.AddScoped<ThemeService>();
builder.Services.AddScoped<PreferencesService>();
builder.Services.AddScoped<MessageStoreService>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<TabLeaderService>();
builder.Services.AddScoped<NotificationService>();
//builder.Services.AddSignalR(options =>
//{
//    options.EnableDetailedErrors = true;
//});
var host = builder.Build();

var js = host.Services.GetRequiredService<IJSRuntime>();
var cultureName = await js.InvokeAsync<string>("localStorage.getItem", "app.culture");
var culture = new System.Globalization.CultureInfo(cultureName);
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = culture;
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = culture;

// Sync cookie so server-side rendering matches the selected culture
await js.InvokeVoidAsync("eval",
    $"document.cookie='.AspNetCore.Culture=c={cultureName}|uic={cultureName};path=/;max-age=31536000;samesite=lax'");

await host.RunAsync();
