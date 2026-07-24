using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TecAjalpan.Horarios.Web.Controllers;

[ApiController]
[Route("api/estado")]
public sealed class EstadoController : ControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    public ActionResult<object> Obtener() =>
        Ok(new
        {
            servicio = "TecAjalpan.Horarios",
            estado = "Disponible",
            utc = DateTime.UtcNow
        });
}
