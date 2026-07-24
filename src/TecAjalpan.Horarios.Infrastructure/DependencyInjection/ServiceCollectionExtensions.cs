using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;    
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TecAjalpan.Horarios.Application.Abstractions;
using TecAjalpan.Horarios.Infrastructure.Identity;
using TecAjalpan.Horarios.Infrastructure.Persistence;

namespace TecAjalpan.Horarios.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Horarios")
            ?? throw new InvalidOperationException("Falta ConnectionStrings:Horarios.");

        services.AddSingleton<IFechaHora, FechaHoraSistema>();
        services.AddScoped<AuditoriaSaveChangesInterceptor>();
        services.AddDbContext<ApplicationDbContext>((provider, options) =>
            options.UseSqlServer(connectionString, sql =>
                    sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName))
                .AddInterceptors(provider.GetRequiredService<AuditoriaSaveChangesInterceptor>()));

        services.AddIdentityCore<UsuarioAplicacion>(options =>
            {
                options.Password.RequiredLength = 10;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = false;
            })
            .AddRoles<IdentityRole>()
            .AddSignInManager()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddClaimsPrincipalFactory<UsuarioClaimsPrincipalFactory>()
            .AddDefaultTokenProviders();

        services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddIdentityCookies();


        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "__Host-TecAjalpan.Horarios";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.Path = "/";

            options.SlidingExpiration = true;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(30);

            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };

            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });

        //services.ConfigureApplicationCookie(options =>
        //{
        //    options.Cookie.Name = "__Host-TecAjalpan.Horarios";
        //    options.Cookie.HttpOnly = true;
        //    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        //    options.Cookie.SameSite = SameSiteMode.Strict;
        //    options.SlidingExpiration = true;
        //    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        //    options.Events.OnRedirectToLogin = context =>
        //    {
        //        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        //        return Task.CompletedTask;
        //    };
        //    options.Events.OnRedirectToAccessDenied = context =>
        //    {
        //        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        //        return Task.CompletedTask;
        //    };
        //});

        services.AddScoped<IPeriodoRepository, PeriodoRepository>();
        services.AddScoped<IHorarioRepository, HorarioRepository>();
        return services;
    }
}
