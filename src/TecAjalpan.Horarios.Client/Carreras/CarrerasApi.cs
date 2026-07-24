using System.Net;
using System.Net.Http.Json;
using TecAjalpan.Horarios.Contracts.Carreras;

namespace TecAjalpan.Horarios.Client.Carreras;

public sealed class CarrerasApi(HttpClient httpClient)
{
    public async Task<IReadOnlyList<CarreraDto>> ListarAsync(
        CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<CarreraDto[]>(
            "api/carreras",
            cancellationToken) ?? [];

    public Task<ResultadoCarrera> GuardarAsync(
        Guid? id,
        GuardarCarreraRequest request,
        CancellationToken cancellationToken = default) =>
        EnviarAsync(
            id.HasValue ? HttpMethod.Put : HttpMethod.Post,
            id.HasValue ? $"api/carreras/{id}" : "api/carreras",
            request,
            cancellationToken);

    public Task<ResultadoCarrera> CambiarEstadoAsync(
        CarreraDto carrera,
        CancellationToken cancellationToken = default) =>
        EnviarAsync(
            HttpMethod.Patch,
            $"api/carreras/{carrera.Id}/estado",
            new CambiarEstadoCarreraRequest
            {
                Activo = !carrera.Activo,
                RowVersion = carrera.RowVersion
            },
            cancellationToken);

    private async Task<ResultadoCarrera> EnviarAsync<T>(
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
            var carrera = await response.Content.ReadFromJsonAsync<CarreraDto>(
                cancellationToken: cancellationToken);
            return new ResultadoCarrera(true, null, carrera);
        }

        return new ResultadoCarrera(
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
            return "No tienes permiso para administrar carreras.";
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

public sealed record ResultadoCarrera(
    bool Correcto,
    string? Mensaje,
    CarreraDto? Carrera);
