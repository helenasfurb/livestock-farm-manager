using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Helpers;
using MuuBoi.Application.Interfaces;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Api.Controllers
{
    [ApiController]
    [Route("api/stock")]
    [Authorize]
    public class StockController : ControllerBase
    {
        private readonly IStockItemService _service;

        public StockController(IStockItemService service)
        {
            _service = service;
        }

        [HttpGet("dashboard")]
        public async Task<ActionResult<StockDashboardDto>> GetDashboard([FromQuery] StockDashboardFilterDto filter)
        {
            var dashboard = await _service.GetDashboardAsync(filter);
            return Ok(dashboard);
        }

        [HttpGet("alerts")]
        public async Task<ActionResult<IEnumerable<StockAlertDto>>> GetAlerts()
        {
            var alerts = await _service.GetAlertsAsync();
            return Ok(alerts);
        }

        [HttpGet("movement-types")]
        public ActionResult<IEnumerable<LookupDto>> GetMovementTypes()
            => Ok(EnumHelper.ToLookup<StockMovementType>());

        [HttpGet("movement-reasons")]
        public ActionResult<IEnumerable<LookupDto>> GetMovementReasons()
            => Ok(EnumHelper.ToLookup<StockMovementReason>());
    }
}
