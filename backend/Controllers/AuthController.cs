using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web;
using System.Security.Claims;

namespace Predict.Backend.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        [HttpGet("me")]
        public IActionResult GetMe()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return Unauthorized();
            }

            return Ok(new
            {
                Name = User.GetDisplayName(),
                Email = User.FindFirst(ClaimTypes.Email)?.Value ?? User.FindFirst("preferred_username")?.Value,
                TenantId = User.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid")?.Value
            });
        }
    }
}
