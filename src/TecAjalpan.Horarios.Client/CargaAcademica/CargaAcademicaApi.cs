using System.Net;
using System.Net.Http.Json;
using TecAjalpan.Horarios.Contracts.CargaAcademica;

namespace TecAjalpan.Horarios.Client.CargaAcademica;

public sealed class CargaAcademicaApi(HttpClient httpClient)
{
    public async Task<CargaAcademicaCatalogosDto> ObtenerCatalogosAsync(
        CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<CargaAcademicaCatalogosDto>(
            "api/carga-academica/catalogos", cancellationToken)
        ?? new([], [], [], []);

    public async Task<ResultadoCarga<CargaConfiguracionDto>> ObtenerAsync(
        Guid periodoId,
        Guid carreraId,
        Guid modalidadId,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(
            $"api/carga-academica?periodoId={periodoId}&carreraId={carreraId}&modalidadId={modalidadId}",
            cancellationToken);
        return await LeerRespuesta<CargaConfiguracionDto>(response, cancellationToken);
    }

    public Task<ResultadoCarga<CargaMateriaDto>> GuardarAsync(
        GuardarCargaAcademicaRequest request,
        CancellationToken cancellationToken = default) =>
        EnviarAsync<GuardarCargaAcademicaRequest, CargaMateriaDto>(
            HttpMethod.Put,
            $"api/carga-academica/materias/{request.OfertaMateriaId}",
            request,
            cancellationToken);

    public Task<ResultadoCarga<CargaMateriaDto>> AutorizarAsync(
        Guid ofertaMateriaId,
        AutorizarCargaAcademicaRequest request,
        CancellationToken cancellationToken = default) =>
        EnviarAsync<AutorizarCargaAcademicaRequest, CargaMateriaDto>(
            HttpMethod.Post,
            $"api/carga-academica/materias/{ofertaMateriaId}/autorizar",
            request,
            cancellationToken);

    public Task<ResultadoCarga<CargaAutorizacionDocenteDto>> GuardarCargaAutorizadaAsync(
        GuardarCargaAutorizadaRequest request,
        CancellationToken cancellationToken = default) =>
        EnviarAsync<GuardarCargaAutorizadaRequest, CargaAutorizacionDocenteDto>(
            HttpMethod.Put,
            $"api/carga-academica/docentes/{request.DocenteId}/autorizacion/{request.PeriodoId}",
            request,
            cancellationToken);

    private async Task<ResultadoCarga<TResponse>> EnviarAsync<TRequest, TResponse>(
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
        return await LeerRespuesta<TResponse>(response, cancellationToken);
    }

    private async Task<string> ObtenerAntiforgeryAsync(CancellationToken cancellationToken)
    {
        var response = await httpClient.GetFromJsonAsync<AntiforgeryDto>(
            "api/seguridad/antiforgery", cancellationToken);
        return response?.Token ?? throw new InvalidOperationException(
            "No fue posible obtener el token antifalsificación.");
    }

    private static async Task<ResultadoCarga<T>> LeerRespuesta<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return new(true, null, await response.Content.ReadFromJsonAsync<T>(
                cancellationToken: cancellationToken));
        }
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return new(false, "Tu sesión terminó. Vuelve a iniciar sesión.", default);
        if (response.StatusCode == HttpStatusCode.Forbidden)
            return new(false, "No tienes permiso para administrar la carga de esa carrera.", default);
        try
        {
            var error = await response.Content.ReadFromJsonAsync<ApiError>(
                cancellationToken: cancellationToken);
            return new(false, error?.Mensaje ?? "No fue posible completar la operación.", default);
        }
        catch
        {
            return new(false, "No fue posible completar la operación.", default);
        }
    }

    private sealed record AntiforgeryDto(string Token);
    private sealed record ApiError(string Mensaje);
}

public sealed record ResultadoCarga<T>(bool Correcto, string? Mensaje, T? Valor);
