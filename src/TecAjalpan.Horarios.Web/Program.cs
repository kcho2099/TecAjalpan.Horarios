using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecAjalpan.Horarios.Application.Abstractions;
using TecAjalpan.Horarios.Application.Security;
using TecAjalpan.Horarios.Infrastructure.DependencyInjection;
using TecAjalpan.Horarios.Web.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUsuarioActual, UsuarioActual>();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    // options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-XSRF-TOKEN";
    options.Cookie.Name = "TecAjalpan.XSRF";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.Path = "/";
});
//builder.Services.AddAntiforgery(options =>
//{
//    options.HeaderName = "X-XSRF-TOKEN";
//    options.Cookie.Name = "__Host-TecAjalpan.XSRF";
//    options.Cookie.HttpOnly = true;
//    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
//    options.Cookie.SameSite = SameSiteMode.Strict;
//});
builder.Services.AddProblemDetails();
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Politicas.AdministrarUsuarios,
        policy => policy.RequireRole(Roles.Administrador))
    .AddPolicy(Politicas.AdministrarPeriodos,
        policy => policy.RequireRole(Roles.Administrador, Roles.Secretaria))
    .AddPolicy(Politicas.GenerarHorario,
        policy => policy.RequireRole(Roles.Administrador, Roles.Jefatura))
    .AddPolicy(Politicas.AprobarHorario,
        policy => policy.RequireRole(Roles.Subdireccion))
    .AddPolicy(Politicas.PublicarHorario,
        policy => policy.RequireRole(Roles.Administrador, Roles.Subdireccion))
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("autenticacion", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "local",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseHsts();
app.UseHttpsRedirection();
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapFallbackToFile("index.html").AllowAnonymous();

await SemillaIdentidad.InicializarAsync(app.Services, app.Configuration);
await app.RunAsync();
