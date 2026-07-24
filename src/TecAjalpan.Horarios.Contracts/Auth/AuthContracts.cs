namespace TecAjalpan.Horarios.Contracts.Auth;

public sealed record LoginRequest(string Correo, string Contrasena);

public sealed record CambiarContrasenaRequest(
    string ContrasenaActual,
    string ContrasenaNueva);

public sealed record UsuarioSesionDto(
    string Id,
    string Correo,
    string Nombre,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<Guid> Carreras,
    bool DebeCambiarContrasena);

public sealed record ResultadoOperacionDto(bool Exitoso, string Mensaje);
