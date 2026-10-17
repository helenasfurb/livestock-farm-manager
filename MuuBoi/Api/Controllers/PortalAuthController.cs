using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Helpers;
using MuuBoi.Application.Interfaces;

namespace MuuBoi.Api.Controllers
{
    [ApiController]
    [Route("api/portal/auth")]
    public class PortalAuthController : ControllerBase
    {
        private readonly IPortalAuthService _portalAuthService;

        public PortalAuthController(IPortalAuthService portalAuthService)
        {
            _portalAuthService = portalAuthService;
        }

        [HttpPost("login")]
        [EnableRateLimiting(PortalAuthDefaults.LoginRateLimit)]
        public ActionResult<PortalTokenDto> Login([FromBody] PortalLoginDto dto)
        {
            return Ok(_portalAuthService.Login(dto));
        }
    }
}
