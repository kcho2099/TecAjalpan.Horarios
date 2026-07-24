using System.Net.Http.Json;
using TecAjalpan.Horarios.Contracts.Auditoria;

namespace TecAjalpan.Horarios.Client.Auditoria;

public sealed class BitacoraApi(HttpClient httpClient)
{
    public async Task<IReadOnlyList<BitacoraDto>> ListarAsync(
        CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<BitacoraDto[]>(
            "api/bitacora",
            cancellationToken) ?? [];
}
