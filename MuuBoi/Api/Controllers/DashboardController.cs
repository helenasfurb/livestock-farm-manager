using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MuuBoi.Application.Interfaces;
using MuuBoi.Application.DTOs;

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

        [HttpGet("productive")]
        public async Task<ActionResult<ProductiveDashboardDto>> GetProductive([FromQuery] ProductiveDashboardFilterDto filter)
        {
            var dashboard = await _dashboardService.GetProductiveDashboardAsync(filter);
            return Ok(dashboard);
        }

        [HttpGet("reproductive")]
        public async Task<ActionResult<ReproductiveDashboardDto>> GetReproductive([FromQuery] ReproductiveDashboardFilterDto filter)
        {
            var dashboard = await _dashboardService.GetReproductiveDashboardAsync(filter);
            return Ok(dashboard);
        }
    }
}
