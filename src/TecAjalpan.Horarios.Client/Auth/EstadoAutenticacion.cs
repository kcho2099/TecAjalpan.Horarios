using System.Security.Claims;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using TecAjalpan.Horarios.Contracts.Auth;

namespace TecAjalpan.Horarios.Client.Auth;

public sealed class EstadoAutenticacion(HttpClient httpClient) : AuthenticationStateProvider
{
    private static readonly ClaimsPrincipal Anonimo =
       new(new ClaimsIdentity());

    private UsuarioSesionDto? usuario;
    private Task<AuthenticationState>? tareaInicializacion;

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        return tareaInicializacion ??= CargarEstadoInicialAsync();
    }

    private async Task<AuthenticationState> CargarEstadoInicialAsync()
    {
        try
        {
            using var cancellationTokenSource =
                new CancellationTokenSource(TimeSpan.FromSeconds(15));

            using var response = await httpClient.GetAsync(
                "api/auth/me",
                cancellationTokenSource.Token);

            usuario = response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<UsuarioSesionDto>(
                    cancellationToken: cancellationTokenSource.Token)
                : null;
        }
        catch (HttpRequestException)
        {
            usuario = null;
        }
        catch (OperationCanceledException)
        {
            usuario = null;
        }
        catch (System.Text.Json.JsonException)
        {
            usuario = null;
        }

        return CrearEstado(usuario);
    }

    public void EstablecerSesion(UsuarioSesionDto? sesion)
    {
        usuario = sesion;
        tareaInicializacion = Task.FromResult(CrearEstado(usuario));

        NotifyAuthenticationStateChanged(tareaInicializacion);
    }

    private static AuthenticationState CrearEstado(
        UsuarioSesionDto? sesion)
    {
        if (sesion is null)
        {
            return new AuthenticationState(Anonimo);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, sesion.Id),
            new(ClaimTypes.Name, sesion.Nombre),
            new(ClaimTypes.Email, sesion.Correo),
            new(
                "debe_cambiar_contrasena",
                sesion.DebeCambiarContrasena.ToString())
        };

        claims.AddRange(sesion.Roles.Select(
            rol => new Claim(ClaimTypes.Role, rol)));

        claims.AddRange(sesion.Carreras.Select(
            carrera => new Claim("carrera", carrera.ToString())));

        return new AuthenticationState(
            new ClaimsPrincipal(
                new ClaimsIdentity(claims, "Cookie")));
    }
}
