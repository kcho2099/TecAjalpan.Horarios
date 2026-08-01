using TecAjalpan.Horarios.Domain.Enums;

namespace TecAjalpan.Horarios.Domain.Rules;

public static class ReglasModalidad
{
    public static bool PermiteProgramar(
        TipoModalidad modalidad,
        DiaAcademico dia) => modalidad switch
        {
            TipoModalidad.Escolarizada => dia is >= DiaAcademico.Lunes
                and <= DiaAcademico.Viernes,
            TipoModalidad.Sabatina => dia == DiaAcademico.Sabado,
            _ => false
        };
}
