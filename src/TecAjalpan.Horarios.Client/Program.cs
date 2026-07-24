using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using TecAjalpan.Horarios.Client;
using TecAjalpan.Horarios.Client.Auth;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<CookieHandler>();
builder.Services.AddScoped(sp =>
{
    var handler = sp.GetRequiredService<CookieHandler>();
    handler.InnerHandler = new HttpClientHandler();
    return new HttpClient(handler)
    {
        BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
    };
});
builder.Services.AddScoped<AuthApi>();
builder.Services.AddScoped<EstadoAutenticacion>();
builder.Services.AddScoped<AuthenticationStateProvider>(
    sp => sp.GetRequiredService<EstadoAutenticacion>());

await builder.Build().RunAsync();
