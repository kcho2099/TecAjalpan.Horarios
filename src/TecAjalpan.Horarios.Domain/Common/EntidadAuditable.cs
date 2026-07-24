namespace TecAjalpan.Horarios.Domain.Common;

public abstract class EntidadAuditable
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public string UsuarioCrea { get; set; } = string.Empty;
    public DateTime FechaCrea { get; set; } = DateTime.UtcNow;
    public string? UsuarioModifica { get; set; }
    public DateTime? FechaModifica { get; set; }
    public bool Eliminado { get; set; }
    public string? UsuarioElimina { get; set; }
    public DateTime? FechaElimina { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public abstract class CatalogoAuditable : EntidadAuditable
{
    public string Clave { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
}
