using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Helpers;
using MuuBoi.Application.Interfaces;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Api.Controllers
{
    [ApiController]
    [Route("api/milk-productions")]
    [Authorize]
    public class MilkProductionsController : ControllerBase
    {
        private readonly IMilkProductionService _service;

        public MilkProductionsController(IMilkProductionService service)
        {
            _service = service;
        }

        [HttpGet("milkings")]
        public ActionResult<IEnumerable<LookupDto>> GetMilkings()
            => Ok(EnumHelper.ToLookup<MilkingShift>());

        [HttpGet]
        public async Task<ActionResult<IEnumerable<MilkProductionDayDto>>> GetAll([FromQuery] MilkProductionFilterDto filter)
        {
            var days = await _service.GetAllAsync(filter);
            return Ok(days);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<MilkProductionDto>> GetById(int id)
        {
            var production = await _service.GetByIdAsync(id);
            return Ok(production);
        }

        [HttpPost]
        public async Task<ActionResult<MilkProductionDto>> Create([FromBody] MilkProductionCreateDto dto)
        {
            var created = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPatch("{id:int}")]
        public async Task<ActionResult<MilkProductionDto>> Update(int id, [FromBody] MilkProductionUpdateDto dto)
        {
            var updated = await _service.UpdateAsync(id, dto);
            return Ok(updated);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Deactivate(int id)
        {
            await _service.DeactivateAsync(id);
            return NoContent();
        }
    }
}
