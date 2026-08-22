using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Helpers;
using MuuBoi.Application.Interfaces;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Api.Controllers
{
    [ApiController]
    [Authorize]
    public class BreedingEventsController : ControllerBase
    {
        private readonly IBreedingEventService _service;

        public BreedingEventsController(IBreedingEventService service)
        {
            _service = service;
        }

        [HttpGet("api/breeding-events/reproduction-types")]
        public ActionResult<IEnumerable<LookupDto>> GetReproductionTypes()
            => Ok(EnumHelper.ToLookup<ReproductionType>());

        [HttpGet("api/breeding-events/statuses")]
        public ActionResult<IEnumerable<LookupDto>> GetStatuses()
            => Ok(EnumHelper.ToLookup<ReproductiveEventStatus>());

        [HttpGet("api/animals/{animalId:int}/breeding-events")]
        public async Task<ActionResult<IEnumerable<BreedingEventListItemDto>>> GetByAnimal(int animalId)
        {
            var events = await _service.GetByAnimalIdAsync(animalId);
            return Ok(events);
        }

        [HttpPost("api/animals/{animalId:int}/breeding-events")]
        public async Task<ActionResult<BreedingEventDto>> Create(int animalId, [FromBody] BreedingEventCreateDto dto)
        {
            var created = await _service.CreateAsync(animalId, dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpGet("api/breeding-events")]
        public async Task<ActionResult<IEnumerable<BreedingEventListItemDto>>> GetAll([FromQuery] BreedingEventFilterDto filter)
        {
            var events = await _service.GetAllAsync(filter);
            return Ok(events);
        }

        [HttpGet("api/breeding-events/{id:int}")]
        public async Task<ActionResult<BreedingEventDto>> GetById(int id)
        {
            var ev = await _service.GetByIdAsync(id);
            return Ok(ev);
        }

        [HttpPatch("api/breeding-events/{id:int}")]
        public async Task<ActionResult<BreedingEventDto>> Update(int id, [FromBody] BreedingEventUpdateDto dto)
        {
            var updated = await _service.UpdateAsync(id, dto);
            return Ok(updated);
        }

        [HttpPatch("api/breeding-events/{id:int}/status")]
        public async Task<ActionResult<BreedingEventDto>> UpdateStatus(int id, [FromBody] BreedingEventStatusUpdateDto dto)
        {
            var updated = await _service.UpdateStatusAsync(id, dto);
            return Ok(updated);
        }

        [HttpDelete("api/breeding-events/{id:int}")]
        public async Task<IActionResult> Deactivate(int id)
        {
            await _service.DeactivateAsync(id);
            return NoContent();
        }
    }
}
