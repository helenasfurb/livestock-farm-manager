using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MuuBoi.Application.Interfaces;
using MuuBoi.DTOs;

namespace MuuBoi.Api.Controllers
{
    [ApiController]
    [Route("api/dashboard")]
    [Authorize]
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;

        public DashboardController(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        [HttpGet]
        public async Task<ActionResult<DashboardDto>> Get()
        {
            var dashboard = await _dashboardService.GetDashboardAsync();
            return Ok(dashboard);
        }
    }
}
