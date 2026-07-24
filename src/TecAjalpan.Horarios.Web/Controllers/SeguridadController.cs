using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TecAjalpan.Horarios.Web.Controllers;

[ApiController]
[Route("api/seguridad")]
public sealed class SeguridadController(IAntiforgery antiforgery) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("antiforgery")]
    public ActionResult<object> ObtenerToken()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new { token = tokens.RequestToken });
    }
}
