using System.Net;
using System.Net.Http.Json;
using TecAjalpan.Horarios.Contracts.PlanesEstudio;

namespace TecAjalpan.Horarios.Client.PlanesEstudio;

public sealed class PlanesEstudioApi(HttpClient httpClient)
{
    public async Task<IReadOnlyList<ModalidadPlanDto>> ModalidadesAsync() =>
        await httpClient.GetFromJsonAsync<ModalidadPlanDto[]>("api/planes-estudio/modalidades") ?? [];

    public async Task<IReadOnlyList<ReticulaDto>> ReticulasAsync(Guid carreraId) =>
        await httpClient.GetFromJsonAsync<ReticulaDto[]>($"api/planes-estudio/reticulas?carreraId={carreraId}") ?? [];

    public async Task<IReadOnlyList<MateriaDto>> MateriasAsync(Guid reticulaId, byte? semestre = null) =>
        await httpClient.GetFromJsonAsync<MateriaDto[]>(
            $"api/planes-estudio/materias?reticulaId={reticulaId}" +
            (semestre.HasValue ? $"&semestre={semestre}" : string.Empty)) ?? [];

    public Task<ResultadoPlan<ReticulaDto>> GuardarReticulaAsync(Guid? id, GuardarReticulaRequest request) =>
        EnviarAsync<ReticulaDto>(id.HasValue ? HttpMethod.Put : HttpMethod.Post,
            id.HasValue ? $"api/planes-estudio/reticulas/{id}" : "api/planes-estudio/reticulas", request);

    public Task<ResultadoPlan<MateriaDto>> GuardarMateriaAsync(Guid? id, GuardarMateriaRequest request) =>
        EnviarAsync<MateriaDto>(id.HasValue ? HttpMethod.Put : HttpMethod.Post,
            id.HasValue ? $"api/planes-estudio/materias/{id}" : "api/planes-estudio/materias", request);

    public Task<ResultadoPlan<ReticulaDto>> EstadoReticulaAsync(ReticulaDto item) =>
        EnviarAsync<ReticulaDto>(HttpMethod.Patch, $"api/planes-estudio/reticulas/{item.Id}/estado",
            new CambiarEstadoPlanRequest { Activo = !item.Activo, RowVersion = item.RowVersion });

    public Task<ResultadoPlan<MateriaDto>> EstadoMateriaAsync(MateriaDto item) =>
        EnviarAsync<MateriaDto>(HttpMethod.Patch, $"api/planes-estudio/materias/{item.Id}/estado",
            new CambiarEstadoPlanRequest { Activo = !item.Activo, RowVersion = item.RowVersion });

    private async Task<ResultadoPlan<T>> EnviarAsync<T>(HttpMethod metodo, string url, object contenido)
    {
        var antiforgery = await httpClient.GetFromJsonAsync<AntiforgeryDto>("api/seguridad/antiforgery");
        using var message = new HttpRequestMessage(metodo, url) { Content = JsonContent.Create(contenido) };
        message.Headers.TryAddWithoutValidation("X-XSRF-TOKEN", antiforgery?.Token);
        using var response = await httpClient.SendAsync(message);
        if (response.IsSuccessStatusCode)
            return new(true, null, await response.Content.ReadFromJsonAsync<T>());
        if (response.StatusCode == HttpStatusCode.Forbidden)
            return new(false, "No tienes permiso para modificar planes de estudio.", default);
        try
        {
            var error = await response.Content.ReadFromJsonAsync<ApiError>();
            return new(false, error?.Mensaje ?? "No fue posible completar la operación.", default);
        }
        catch { return new(false, "No fue posible completar la operación.", default); }
    }

    private sealed record AntiforgeryDto(string Token);
    private sealed record ApiError(string Mensaje);
}

public sealed record ResultadoPlan<T>(bool Correcto, string? Mensaje, T? Datos);
