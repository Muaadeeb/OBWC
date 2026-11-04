using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor;
using MudBlazor.Services;
using OBWC.Client;
using OBWC.Client.Scheduling;
using OBWC.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped<ApiOrigin>();
builder.Services.AddScoped<TokenStore>();
builder.Services.AddScoped<AuthHeaderHandler>();
builder.Services.AddScoped<ObwcAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(provider => provider.GetRequiredService<ObwcAuthenticationStateProvider>());
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped(provider =>
{
    var origin = provider.GetRequiredService<ApiOrigin>();
    var handler = provider.GetRequiredService<AuthHeaderHandler>();
    handler.InnerHandler = new HttpClientHandler();
    return new HttpClient(handler) { BaseAddress = origin.Base };
});
builder.Services.AddScoped<AuthApi>();
builder.Services.AddScoped<ClubApi>();
builder.Services.AddScoped<ScheduleApi>();
builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.TopCenter;
    config.SnackbarConfiguration.VisibleStateDuration = 5000;
    config.SnackbarConfiguration.ShowCloseIcon = true;
});

await builder.Build().RunAsync();
