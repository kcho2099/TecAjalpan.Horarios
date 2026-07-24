namespace TecAjalpan.Horarios.Application.Security;

public static class Roles
{
    public const string Administrador = nameof(Administrador);
    public const string Secretaria = "Secretaría";
    public const string Jefatura = nameof(Jefatura);
    public const string Subdireccion = "Subdirección Académica";
    public const string Consulta = nameof(Consulta);

    public static readonly string[] Todos =
    [
        Administrador,
        Secretaria,
        Jefatura,
        Subdireccion,
        Consulta
    ];
}

public static class Politicas
{
    public const string AdministrarUsuarios = nameof(AdministrarUsuarios);
    public const string AdministrarPeriodos = nameof(AdministrarPeriodos);
    public const string AdministrarCarrera = nameof(AdministrarCarrera);
    public const string GenerarHorario = nameof(GenerarHorario);
    public const string AprobarHorario = nameof(AprobarHorario);
    public const string PublicarHorario = nameof(PublicarHorario);
    public const string ConsultarPublicado = nameof(ConsultarPublicado);
}
