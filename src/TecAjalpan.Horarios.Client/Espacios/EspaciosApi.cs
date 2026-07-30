using System.Net;
using System.Net.Http.Json;
using TecAjalpan.Horarios.Contracts.Espacios;

namespace TecAjalpan.Horarios.Client.Espacios;

public sealed class EspaciosApi(HttpClient httpClient)
{
    public async Task<EspaciosCatalogoDto> ListarAsync(
        CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<EspaciosCatalogoDto>(
            "api/espacios",
            cancellationToken) ?? new([], []);

    public Task<ResultadoEspacio> GuardarAsync(
        Guid? id,
        GuardarEspacioRequest request,
        CancellationToken cancellationToken = default) =>
        EnviarAsync(
            id.HasValue ? HttpMethod.Put : HttpMethod.Post,
            id.HasValue ? $"api/espacios/{id}" : "api/espacios",
            request,
            cancellationToken);

    public Task<ResultadoEspacio> CambiarEstadoAsync(
        EspacioDto espacio,
        CancellationToken cancellationToken = default) =>
        EnviarAsync(
            HttpMethod.Patch,
            $"api/espacios/{espacio.Id}/estado",
            new CambiarEstadoEspacioRequest
            {
                Activo = !espacio.Activo,
                RowVersion = espacio.RowVersion
            },
            cancellationToken);

    private async Task<ResultadoEspacio> EnviarAsync<T>(
        HttpMethod metodo,
        string url,
        T contenido,
        CancellationToken cancellationToken)
    {
        var token = await ObtenerAntiforgeryAsync(cancellationToken);
        using var message = new HttpRequestMessage(metodo, url)
        {
            Content = JsonContent.Create(contenido)
        };
        message.Headers.TryAddWithoutValidation("X-XSRF-TOKEN", token);

        using var response = await httpClient.SendAsync(message, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var espacio = await response.Content.ReadFromJsonAsync<EspacioDto>(
                cancellationToken: cancellationToken);
            return new(true, null, espacio);
        }

        return new(
            false,
            await LeerMensajeAsync(response, cancellationToken),
            null);
    }

    private async Task<string> ObtenerAntiforgeryAsync(
        CancellationToken cancellationToken)
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
            return "No tienes permiso para administrar espacios de esa carrera.";
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

public sealed record ResultadoEspacio(
    bool Correcto,
    string? Mensaje,
    EspacioDto? Espacio);
