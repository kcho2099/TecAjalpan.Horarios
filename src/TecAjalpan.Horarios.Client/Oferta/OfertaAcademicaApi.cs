using System.Net;
using System.Net.Http.Json;
using TecAjalpan.Horarios.Contracts.Oferta;

namespace TecAjalpan.Horarios.Client.Oferta;

public sealed class OfertaAcademicaApi(HttpClient httpClient)
{
    public async Task<OfertaCatalogosDto> ObtenerCatalogosAsync(
        CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<OfertaCatalogosDto>(
            "api/oferta-academica/catalogos", cancellationToken)
        ?? new([], [], [], []);

    public async Task<IReadOnlyList<PeriodoCarreraOfertaDto>> ListarAsync(
        Guid periodoId,
        CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<PeriodoCarreraOfertaDto[]>(
            $"api/oferta-academica?periodoId={periodoId}", cancellationToken) ?? [];

    public async Task<IReadOnlyList<MateriaDisponibleOfertaDto>> MateriasDisponiblesAsync(
        Guid periodoCarreraId,
        byte semestre,
        CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<MateriaDisponibleOfertaDto[]>(
            $"api/oferta-academica/materias-disponibles?periodoCarreraId={periodoCarreraId}&semestre={semestre}",
            cancellationToken) ?? [];

    public Task<ResultadoOferta<PeriodoCarreraOfertaDto>> CrearConfiguracionAsync(
        CrearPeriodoCarreraRequest request,
        CancellationToken cancellationToken = default) =>
        EnviarAsync<CrearPeriodoCarreraRequest, PeriodoCarreraOfertaDto>(
            HttpMethod.Post, "api/oferta-academica/configuraciones", request, cancellationToken);

    public Task<ResultadoOferta<GrupoOfertaDto>> GuardarGrupoAsync(
        Guid? grupoId,
        GuardarGrupoOfertaRequest request,
        CancellationToken cancellationToken = default) =>
        EnviarAsync<GuardarGrupoOfertaRequest, GrupoOfertaDto>(
            grupoId.HasValue ? HttpMethod.Put : HttpMethod.Post,
            grupoId.HasValue
                ? $"api/oferta-academica/grupos/{grupoId}"
                : "api/oferta-academica/grupos",
            request, cancellationToken);

    public Task<ResultadoOferta<GrupoOfertaDto>> GuardarMateriasAsync(
        Guid grupoId,
        GuardarMateriasOfertaRequest request,
        CancellationToken cancellationToken = default) =>
        EnviarAsync<GuardarMateriasOfertaRequest, GrupoOfertaDto>(
            HttpMethod.Put, $"api/oferta-academica/grupos/{grupoId}/materias",
            request, cancellationToken);

    public Task<ResultadoOferta<bool>> EliminarConfiguracionAsync(
        Guid configuracionId,
        string rowVersion,
        CancellationToken cancellationToken = default) =>
        EnviarSinRespuestaAsync(
            HttpMethod.Delete,
            $"api/oferta-academica/configuraciones/{configuracionId}",
            new EliminarOfertaRequest { RowVersion = rowVersion },
            cancellationToken);

    public Task<ResultadoOferta<bool>> EliminarGrupoAsync(
        Guid grupoId,
        string rowVersion,
        CancellationToken cancellationToken = default) =>
        EnviarSinRespuestaAsync(
            HttpMethod.Delete,
            $"api/oferta-academica/grupos/{grupoId}",
            new EliminarOfertaRequest { RowVersion = rowVersion },
            cancellationToken);

    private async Task<ResultadoOferta<TResponse>> EnviarAsync<TRequest, TResponse>(
        HttpMethod metodo,
        string url,
        TRequest contenido,
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
            return new(true, null,
                await response.Content.ReadFromJsonAsync<TResponse>(
                    cancellationToken: cancellationToken));
        }
        return new(false, await LeerMensajeAsync(response, cancellationToken), default);
    }

    private async Task<ResultadoOferta<bool>> EnviarSinRespuestaAsync<TRequest>(
        HttpMethod metodo,
        string url,
        TRequest contenido,
        CancellationToken cancellationToken)
    {
        var token = await ObtenerAntiforgeryAsync(cancellationToken);
        using var message = new HttpRequestMessage(metodo, url)
        {
            Content = JsonContent.Create(contenido)
        };
        message.Headers.TryAddWithoutValidation("X-XSRF-TOKEN", token);
        using var response = await httpClient.SendAsync(message, cancellationToken);
        return response.IsSuccessStatusCode
            ? new(true, null, true)
            : new(false, await LeerMensajeAsync(response, cancellationToken), false);
    }

    private async Task<string> ObtenerAntiforgeryAsync(CancellationToken cancellationToken)
    {
        var response = await httpClient.GetFromJsonAsync<AntiforgeryDto>(
            "api/seguridad/antiforgery", cancellationToken);
        return response?.Token ?? throw new InvalidOperationException(
            "No fue posible obtener el token antifalsificación.");
    }

    private static async Task<string> LeerMensajeAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return "Tu sesión terminó. Vuelve a iniciar sesión.";
        if (response.StatusCode == HttpStatusCode.Forbidden)
            return "No tienes permiso para configurar la oferta académica.";
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

public sealed record ResultadoOferta<T>(
    bool Correcto,
    string? Mensaje,
    T? Valor);
