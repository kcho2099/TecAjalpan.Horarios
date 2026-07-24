using System.Net;
using System.Net.Http.Json;
using TecAjalpan.Horarios.Contracts.Carreras;
using TecAjalpan.Horarios.Contracts.Docentes;

namespace TecAjalpan.Horarios.Client.Docentes;

public sealed class DocentesApi(HttpClient httpClient)
{
    public async Task<IReadOnlyList<DocenteDto>> ListarAsync(
        CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<DocenteDto[]>(
            "api/docentes",
            cancellationToken) ?? [];

    public async Task<IReadOnlyList<CarreraDto>> ListarCarrerasDisponiblesAsync(
        CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<CarreraDto[]>(
            "api/docentes/carreras-disponibles",
            cancellationToken) ?? [];

    public Task<ResultadoDocente> GuardarAsync(
        Guid? id,
        GuardarDocenteRequest request,
        CancellationToken cancellationToken = default) =>
        EnviarAsync(
            id.HasValue ? HttpMethod.Put : HttpMethod.Post,
            id.HasValue ? $"api/docentes/{id}" : "api/docentes",
            request,
            cancellationToken);

    public Task<ResultadoDocente> CambiarEstadoAsync(
        DocenteDto docente,
        CancellationToken cancellationToken = default) =>
        EnviarAsync(
            HttpMethod.Patch,
            $"api/docentes/{docente.Id}/estado",
            new CambiarEstadoDocenteRequest
            {
                Activo = !docente.Activo,
                RowVersion = docente.RowVersion
            },
            cancellationToken);

    private async Task<ResultadoDocente> EnviarAsync<T>(
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
            var docente = await response.Content.ReadFromJsonAsync<DocenteDto>(
                cancellationToken: cancellationToken);
            return new ResultadoDocente(true, null, docente);
        }

        return new ResultadoDocente(
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
            return "No tienes permiso para administrar este docente.";
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

public sealed record ResultadoDocente(
    bool Correcto,
    string? Mensaje,
    DocenteDto? Docente);
