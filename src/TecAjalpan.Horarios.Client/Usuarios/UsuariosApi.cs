using System.Net;
using System.Net.Http.Json;
using TecAjalpan.Horarios.Contracts.Usuarios;

namespace TecAjalpan.Horarios.Client.Usuarios;

public sealed class UsuariosApi(HttpClient httpClient)
{
    public async Task<IReadOnlyList<UsuarioDto>> ListarAsync(
        CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<UsuarioDto[]>(
            "api/usuarios",
            cancellationToken) ?? [];

    public async Task<CatalogosUsuarioDto> ObtenerCatalogosAsync(
        CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<CatalogosUsuarioDto>(
            "api/usuarios/catalogos",
            cancellationToken)
        ?? new CatalogosUsuarioDto([], []);

    public Task<ResultadoPeticionUsuario> GuardarAsync(
        string? id,
        GuardarUsuarioRequest request,
        CancellationToken cancellationToken = default) =>
        EnviarAsync(
            id is null ? HttpMethod.Post : HttpMethod.Put,
            id is null ? "api/usuarios" : $"api/usuarios/{id}",
            request,
            cancellationToken);

    public Task<ResultadoPeticionUsuario> CambiarEstadoAsync(
        string id,
        bool activo,
        CancellationToken cancellationToken = default) =>
        EnviarAsync(
            HttpMethod.Patch,
            $"api/usuarios/{id}/estado",
            new CambiarEstadoUsuarioRequest { Activo = activo },
            cancellationToken);

    public Task<ResultadoPeticionUsuario> AsignarCarrerasAsync(
        string id,
        IReadOnlyCollection<Guid> carreras,
        CancellationToken cancellationToken = default) =>
        EnviarAsync(
            HttpMethod.Patch,
            $"api/usuarios/{id}/carreras",
            new AsignarCarrerasUsuarioRequest
            {
                Carreras = carreras.Distinct().ToList()
            },
            cancellationToken);

    public Task<ResultadoPeticionUsuario> RestablecerContrasenaAsync(
        string id,
        CancellationToken cancellationToken = default) =>
        EnviarAsync<object?>(
            HttpMethod.Post,
            $"api/usuarios/{id}/restablecer-contrasena",
            null,
            cancellationToken);

    private async Task<ResultadoPeticionUsuario> EnviarAsync<T>(
        HttpMethod metodo,
        string url,
        T contenido,
        CancellationToken cancellationToken)
    {
        var token = await ObtenerAntiforgeryAsync(cancellationToken);
        using var message = new HttpRequestMessage(metodo, url);
        if (contenido is not null)
        {
            message.Content = JsonContent.Create(contenido);
        }
        message.Headers.TryAddWithoutValidation("X-XSRF-TOKEN", token);

        using var response = await httpClient.SendAsync(message, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var resultado = await response.Content.ReadFromJsonAsync<ResultadoUsuarioDto>(
                cancellationToken: cancellationToken);
            return new ResultadoPeticionUsuario(true, null, resultado);
        }

        return new ResultadoPeticionUsuario(
            false,
            await LeerMensajeAsync(response, cancellationToken),
            null);
    }

    private async Task<string> ObtenerAntiforgeryAsync(CancellationToken cancellationToken)
    {
        var response = await httpClient.GetFromJsonAsync<AntiforgeryDto>(
            "api/seguridad/antiforgery",
            cancellationToken);
        return response?.Token ?? throw new InvalidOperationException(
            "No fue posible obtener el token antifalsificación.");
    }

    private static async Task<string> LeerMensajeAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return "Tu sesión terminó. Vuelve a iniciar sesión.";
        }
        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            return "No tienes permiso para administrar usuarios.";
        }

        try
        {
            var error = await response.Content.ReadFromJsonAsync<ApiError>(
                cancellationToken: cancellationToken);
            return error?.Mensaje ?? "No fue posible completar la operación.";
        }
        catch
        {
            return "No fue posible completar la operación.";
        }
    }

    private sealed record AntiforgeryDto(string Token);
    private sealed record ApiError(string Mensaje);
}

public sealed record ResultadoPeticionUsuario(
    bool Correcto,
    string? Mensaje,
    ResultadoUsuarioDto? Resultado);
