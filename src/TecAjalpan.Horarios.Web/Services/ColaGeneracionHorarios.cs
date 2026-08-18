using System.Threading.Channels;

namespace TecAjalpan.Horarios.Web.Services;

public interface IColaGeneracionHorarios
{
    ValueTask EncolarAsync(Guid ejecucionId, CancellationToken cancellationToken);
    ValueTask<Guid> DesencolarAsync(CancellationToken cancellationToken);
}

internal sealed class ColaGeneracionHorarios : IColaGeneracionHorarios
{
    private readonly Channel<Guid> cola = Channel.CreateUnbounded<Guid>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    public ValueTask EncolarAsync(
        Guid ejecucionId,
        CancellationToken cancellationToken) =>
        cola.Writer.WriteAsync(ejecucionId, cancellationToken);

    public ValueTask<Guid> DesencolarAsync(CancellationToken cancellationToken) =>
        cola.Reader.ReadAsync(cancellationToken);
}
