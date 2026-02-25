using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ToledoMessage.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddScoped(_ =>
    new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddSingleton<LocalStorageService>();
builder.Services.AddScoped<KeyGenerationService>();

await builder.Build().RunAsync();
