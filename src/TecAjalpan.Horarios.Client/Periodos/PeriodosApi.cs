using System.Net;
using System.Net.Http.Json;
using TecAjalpan.Horarios.Contracts.Periodos;

namespace TecAjalpan.Horarios.Client.Periodos;

public sealed class PeriodosApi(HttpClient httpClient)
{
    public async Task<IReadOnlyList<PeriodoDto>> ListarAsync(
        CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<PeriodoDto[]>("api/periodos", cancellationToken) ?? [];

    public async Task<ResultadoPeriodo> GuardarAsync(
        Guid? id,
        GuardarPeriodoRequest request,
        CancellationToken cancellationToken = default)
    {
        var token = await ObtenerAntiforgeryAsync(cancellationToken);
        using var message = new HttpRequestMessage(
            id.HasValue ? HttpMethod.Put : HttpMethod.Post,
            id.HasValue ? $"api/periodos/{id}" : "api/periodos")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.TryAddWithoutValidation("X-XSRF-TOKEN", token);

        using var response = await httpClient.SendAsync(message, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var periodo = await response.Content.ReadFromJsonAsync<PeriodoDto>(
                cancellationToken: cancellationToken);
            return new ResultadoPeriodo(true, null, periodo);
        }

        return new ResultadoPeriodo(
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

        try
        {
            var error = await response.Content.ReadFromJsonAsync<ApiError>(
                cancellationToken: cancellationToken);
            return error?.Mensaje ?? "No fue posible guardar el periodo.";
        }
        catch
        {
            return "No fue posible guardar el periodo.";
        }
    }

    private sealed record AntiforgeryDto(string Token);
    private sealed record ApiError(string Mensaje);
}

public sealed record ResultadoPeriodo(
    bool Correcto,
    string? Mensaje,
    PeriodoDto? Periodo);
