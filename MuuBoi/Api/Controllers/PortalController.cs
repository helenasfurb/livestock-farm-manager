using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Helpers;
using MuuBoi.Application.Interfaces;

namespace MuuBoi.Api.Controllers
{
    [ApiController]
    [Route("api/portal")]
    [Authorize(Policy = PortalAuthDefaults.Policy)]
    public class PortalController : ControllerBase
    {
        private readonly IPortalMetricsService _portalMetricsService;

        public PortalController(IPortalMetricsService portalMetricsService)
        {
            _portalMetricsService = portalMetricsService;
        }

        [HttpGet("summary")]
        public async Task<ActionResult<PortalSummaryDto>> GetSummary([FromQuery] PortalSummaryFilterDto filter)
        {
            return Ok(await _portalMetricsService.GetSummaryAsync(filter));
        }

        [HttpGet("properties")]
        public async Task<ActionResult<PortalPropertyPageDto>> GetProperties([FromQuery] PortalPropertyFilterDto filter)
        {
            return Ok(await _portalMetricsService.GetPropertiesAsync(filter));
        }
    }
}
