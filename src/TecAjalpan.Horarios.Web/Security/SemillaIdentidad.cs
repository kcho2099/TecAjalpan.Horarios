using Microsoft.AspNetCore.Identity;
using TecAjalpan.Horarios.Application.Security;
using TecAjalpan.Horarios.Infrastructure.Identity;

namespace TecAjalpan.Horarios.Web.Security;

internal static class SemillaIdentidad
{
    public static async Task InicializarAsync(
        IServiceProvider services,
        IConfiguration configuration)
    {
        await using var scope = services.CreateAsyncScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        foreach (var rol in Roles.Todos)
        {
            if (!await roleManager.RoleExistsAsync(rol))
            {
                var resultado = await roleManager.CreateAsync(new IdentityRole(rol));
                if (!resultado.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"No fue posible crear el rol {rol}: " +
                        string.Join("; ", resultado.Errors.Select(x => x.Description)));
                }
            }
        }

        var correo = configuration["BootstrapAdmin:Email"];
        var nombre = configuration["BootstrapAdmin:Nombre"];
        var contrasena = configuration["BootstrapAdmin:ContrasenaTemporal"];
        if (string.IsNullOrWhiteSpace(correo) ||
            string.IsNullOrWhiteSpace(nombre) ||
            string.IsNullOrWhiteSpace(contrasena))
        {
            return;
        }

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<UsuarioAplicacion>>();
        var administrador = await userManager.FindByEmailAsync(correo);
        if (administrador is null)
        {
            administrador = new UsuarioAplicacion
            {
                UserName = correo,
                Email = correo,
                EmailConfirmed = true,
                NombreCompleto = nombre,
                DebeCambiarContrasena = true,
                Activo = true
            };
            var creacion = await userManager.CreateAsync(administrador, contrasena);
            if (!creacion.Succeeded)
            {
                throw new InvalidOperationException(
                    "No fue posible crear el administrador inicial: " +
                    string.Join("; ", creacion.Errors.Select(x => x.Description)));
            }
        }

        if (!await userManager.IsInRoleAsync(administrador, Roles.Administrador))
        {
            await userManager.AddToRoleAsync(administrador, Roles.Administrador);
        }
    }
}
