using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MuuBoi.Application.Interfaces;
using MuuBoi.DTOs;
using MuuBoi.Interfaces;

namespace MuuBoi.Api.Controllers
{
    [ApiController]
    [Route("api/dashboard")]
    [Authorize]
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;
        private readonly ICurrentUserService _currentUserService;

        public DashboardController(IDashboardService dashboardService, ICurrentUserService currentUserService)
        {
            _dashboardService = dashboardService;
            _currentUserService = currentUserService;
        }

        [HttpGet]
        public async Task<ActionResult<DashboardDto>> Get()
        {
            var dashboard = await _dashboardService.GetDashboardAsync(_currentUserService.UserId);
            return Ok(dashboard);
        }
    }
}
