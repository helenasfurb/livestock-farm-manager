using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Interfaces;

namespace MuuBoi.Api.Controllers
{
    [ApiController]
    [Route("api/units-of-measure")]
    [Authorize]
    public class UnitsOfMeasureController : ControllerBase
    {
        private readonly IStockReferenceService _service;

        public UnitsOfMeasureController(IStockReferenceService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<UnitOfMeasureDto>>> GetAll()
        {
            var units = await _service.GetUnitsAsync();
            return Ok(units);
        }
    }
}
