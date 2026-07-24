using System.Net.Http.Json;
using TecAjalpan.Horarios.Contracts.Auth;

namespace TecAjalpan.Horarios.Client.Auth;

public sealed class AuthApi(HttpClient httpClient, EstadoAutenticacion estadoAutenticacion)
{
    private string? tokenAntiforgery;
    public UsuarioSesionDto? SesionActual { get; private set; }

    public async Task<ResultadoOperacionDto> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var mensaje = await CrearPostAsync("api/auth/login", request, cancellationToken);
        if (!mensaje.IsSuccessStatusCode)
        {
            return await LeerErrorAsync(mensaje, cancellationToken);
        }

        var usuario = await mensaje.Content.ReadFromJsonAsync<UsuarioSesionDto>(
            cancellationToken: cancellationToken);
        if (usuario is null)
        {
            return new ResultadoOperacionDto(false, "El servidor no devolvió la sesión.");
        }

        estadoAutenticacion.EstablecerSesion(usuario);
        SesionActual = usuario;
        return new ResultadoOperacionDto(true, "Sesión iniciada.");
    }

    public async Task CerrarSesionAsync(CancellationToken cancellationToken = default)
    {
        var mensaje = await CrearPostAsync<object?>("api/auth/logout", null, cancellationToken);
        mensaje.EnsureSuccessStatusCode();
        SesionActual = null;
        estadoAutenticacion.EstablecerSesion(null);
    }

    public async Task<UsuarioSesionDto?> ObtenerSesionAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync("api/auth/me", cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<UsuarioSesionDto>(
                cancellationToken: cancellationToken)
            : null;
    }

    public async Task<ResultadoOperacionDto> CambiarContrasenaAsync(
        CambiarContrasenaRequest request,
        CancellationToken cancellationToken = default)
    {
        var mensaje = await CrearPostAsync(
            "api/auth/cambiar-contrasena",
            request,
            cancellationToken);
        if (!mensaje.IsSuccessStatusCode)
        {
            return await LeerErrorAsync(mensaje, cancellationToken);
        }

        SesionActual = await ObtenerSesionAsync(cancellationToken);
        estadoAutenticacion.EstablecerSesion(SesionActual);
        return await mensaje.Content.ReadFromJsonAsync<ResultadoOperacionDto>(
                   cancellationToken: cancellationToken)
               ?? new ResultadoOperacionDto(true, "Contraseña actualizada.");
    }

    private async Task<HttpResponseMessage> CrearPostAsync<T>(
        string url,
        T body,
        CancellationToken cancellationToken)
    {
        tokenAntiforgery = await ObtenerAntiforgeryAsync(cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body)
        };

        request.Headers.TryAddWithoutValidation(
            "X-XSRF-TOKEN",
            tokenAntiforgery);

        return await httpClient.SendAsync(request, cancellationToken);
    }

    private async Task<string> ObtenerAntiforgeryAsync(CancellationToken cancellationToken)
    {
        var token = await httpClient.GetFromJsonAsync<AntiforgeryDto>(
            "api/seguridad/antiforgery",
            cancellationToken);
        return token?.Token ?? throw new InvalidOperationException(
            "No fue posible obtener el token antifalsificación.");
    }

    private static async Task<ResultadoOperacionDto> LeerErrorAsync(
        HttpResponseMessage mensaje,
        CancellationToken cancellationToken)
    {
        try
        {
            return await mensaje.Content.ReadFromJsonAsync<ResultadoOperacionDto>(
                       cancellationToken: cancellationToken)
                   ?? new ResultadoOperacionDto(false, "No fue posible iniciar sesión.");
        }
        catch
        {
            return new ResultadoOperacionDto(false, "No fue posible iniciar sesión.");
        }
    }

    private sealed record AntiforgeryDto(string Token);
}
