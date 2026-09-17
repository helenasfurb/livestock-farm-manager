using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Helpers;
using MuuBoi.Application.Interfaces;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Api.Controllers
{
    [ApiController]
    [Route("api/vaccination-events")]
    [Authorize]
    public class VaccinationEventsController : ControllerBase
    {
        private readonly IVaccinationEventService _service;

        public VaccinationEventsController(IVaccinationEventService service)
        {
            _service = service;
        }

        [HttpGet("dose-types")]
        public ActionResult<IEnumerable<LookupDto>> GetDoseTypes()
            => Ok(EnumHelper.ToLookup<DoseType>());

        [HttpGet]
        public async Task<ActionResult<IEnumerable<VaccinationEventListItemDto>>> GetAll(
            [FromQuery] VaccinationEventFilterDto filter)
        {
            var events = await _service.GetAllAsync(filter);
            return Ok(events);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<VaccinationEventDto>> GetById(int id)
        {
            var vaccinationEvent = await _service.GetByIdAsync(id);
            return Ok(vaccinationEvent);
        }

        [HttpPost]
        public async Task<ActionResult<VaccinationEventDto>> Create([FromBody] VaccinationEventCreateDto dto)
        {
            var created = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPost("{id:int}/booster")]
        public async Task<ActionResult<VaccinationEventDto>> CreateBooster(int id, [FromBody] VaccinationBoosterCreateDto dto)
        {
            var created = await _service.CreateBoosterAsync(id, dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPatch("{id:int}")]
        public async Task<ActionResult<VaccinationEventDto>> Update(int id, [FromBody] VaccinationEventUpdateDto dto)
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
