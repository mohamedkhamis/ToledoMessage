using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ToledoMessage.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddScoped(_ =>
    new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddSingleton<LocalStorageService>();
builder.Services.AddScoped<KeyGenerationService>();
builder.Services.AddScoped<SessionService>();
builder.Services.AddScoped<CryptoService>();
builder.Services.AddScoped<MessageEncryptionService>();

await builder.Build().RunAsync();
