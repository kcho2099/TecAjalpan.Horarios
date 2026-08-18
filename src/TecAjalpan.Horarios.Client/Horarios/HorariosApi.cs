using System.Net;
using System.Net.Http.Json;
using TecAjalpan.Horarios.Contracts.Horarios;

namespace TecAjalpan.Horarios.Client.Horarios;

public sealed class HorariosApi(HttpClient httpClient)
{
    public async Task<IReadOnlyCollection<PeriodoGeneracionDto>> ListarPeriodosAsync(
        CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<PeriodoGeneracionDto[]>(
            "api/horarios/periodos",
            cancellationToken) ?? [];

    public async Task<IReadOnlyCollection<HorarioVersionResumenDto>> ListarVersionesAsync(
        Guid periodoId,
        CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<HorarioVersionResumenDto[]>(
            $"api/horarios/periodos/{periodoId}/versiones",
            cancellationToken) ?? [];

    public Task<HorarioDetalleDto?> ObtenerVersionAsync(
        Guid versionId,
        CancellationToken cancellationToken = default) =>
        httpClient.GetFromJsonAsync<HorarioDetalleDto>(
            $"api/horarios/versiones/{versionId}",
            cancellationToken);

    public async Task<ResultadoInicioGeneracion> GenerarAsync(
        GenerarHorarioRequest request,
        CancellationToken cancellationToken = default)
    {
        var antiforgery = await httpClient.GetFromJsonAsync<AntiforgeryDto>(
            "api/seguridad/antiforgery",
            cancellationToken);
        using var message = new HttpRequestMessage(HttpMethod.Post, "api/horarios/generar")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.TryAddWithoutValidation(
            "X-XSRF-TOKEN",
            antiforgery?.Token ?? throw new InvalidOperationException(
                "No fue posible obtener el token antifalsificación."));

        using (var response = await httpClient.SendAsync(message, cancellationToken))
        {
            if (response.IsSuccessStatusCode)
            {
                var resultado = await response.Content.ReadFromJsonAsync<InicioGeneracionDto>(
                    cancellationToken: cancellationToken);
                return new ResultadoInicioGeneracion(true, null, resultado);
            }

            return new ResultadoInicioGeneracion(
                false,
                await LeerMensajeAsync(response, cancellationToken),
                null);
        }
    }

    public Task<EstadoGeneracionDto?> ConsultarEstadoAsync(
        Guid ejecucionId,
        CancellationToken cancellationToken = default) =>
        httpClient.GetFromJsonAsync<EstadoGeneracionDto>(
            $"api/horarios/ejecuciones/{ejecucionId}",
            cancellationToken);

    public async Task<EstadoGeneracionDto?> ObtenerEjecucionActivaAsync(
        Guid periodoId,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(
            $"api/horarios/periodos/{periodoId}/ejecucion-activa",
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.NoContent)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EstadoGeneracionDto>(
            cancellationToken: cancellationToken);
    }

    public async Task<ResultadoOperacionHorario> DescartarAsync(
        Guid versionId,
        CancellationToken cancellationToken = default)
    {
        var antiforgery = await httpClient.GetFromJsonAsync<AntiforgeryDto>(
            "api/seguridad/antiforgery",
            cancellationToken);
        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            $"api/horarios/versiones/{versionId}/descartar");
        message.Headers.TryAddWithoutValidation(
            "X-XSRF-TOKEN",
            antiforgery?.Token ?? throw new InvalidOperationException(
                "No fue posible obtener el token antifalsificación."));

        using var response = await httpClient.SendAsync(message, cancellationToken);
        return response.IsSuccessStatusCode
            ? new ResultadoOperacionHorario(true, null)
            : new ResultadoOperacionHorario(
                false,
                await LeerMensajeAsync(response, cancellationToken));
    }

    private static async Task<string> LeerMensajeAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return "Tu sesión terminó. Vuelve a iniciar sesión.";
        if (response.StatusCode == HttpStatusCode.Forbidden)
            return "No tienes permisos para generar horarios.";

        var texto = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(texto))
            return "No fue posible completar la generación.";

        try
        {
            var error = await response.Content.ReadFromJsonAsync<ApiError>(
                cancellationToken: cancellationToken);
            return error?.Mensaje ?? error?.Detail ?? texto.Trim('"');
        }
        catch
        {
            return texto.Trim('"');
        }
    }

    private sealed record AntiforgeryDto(string Token);
    private sealed record ApiError(string? Mensaje, string? Detail);
}

public sealed record ResultadoInicioGeneracion(
    bool Correcto,
    string? Mensaje,
    InicioGeneracionDto? Resultado);

public sealed record ResultadoOperacionHorario(
    bool Correcto,
    string? Mensaje);
