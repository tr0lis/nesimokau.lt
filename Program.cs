using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using nesimokau.lt;
using nesimokau.lt.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddSingleton<GameCatalogService>();
builder.Services.AddScoped<ProgressService>();
builder.Services.AddScoped<DictationService>();
builder.Services.AddScoped<LeaderboardService>();
builder.Services.AddScoped<AuthService>();

await builder.Build().RunAsync();
