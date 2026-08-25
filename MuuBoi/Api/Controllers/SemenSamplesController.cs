using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Interfaces;

namespace MuuBoi.Api.Controllers
{
    [ApiController]
    [Route("api/semen-samples")]
    [Authorize]
    public class SemenSamplesController : ControllerBase
    {
        private readonly ISemenSampleService _service;
        private readonly ISemenSampleMovementService _movementService;

        public SemenSamplesController(ISemenSampleService service, ISemenSampleMovementService movementService)
        {
            _service = service;
            _movementService = movementService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<SemenSampleListItemDto>>> GetAll([FromQuery] SemenSampleFilterDto filter)
        {
            var samples = await _service.GetAllAsync(filter);
            return Ok(samples);
        }

        [HttpGet("autocomplete")]
        public async Task<ActionResult<IEnumerable<SemenSampleAutocompleteItemDto>>> Autocomplete([FromQuery] string? name)
        {
            var samples = await _service.GetAutocompleteAsync(name);
            return Ok(samples);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<SemenSampleDto>> GetById(int id)
        {
            var sample = await _service.GetByIdAsync(id);
            return Ok(sample);
        }

        [HttpPost]
        public async Task<ActionResult<SemenSampleDto>> Create([FromBody] SemenSampleCreateDto dto)
        {
            var created = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPatch("{id:int}")]
        public async Task<ActionResult<SemenSampleDto>> Update(int id, [FromBody] SemenSampleUpdateDto dto)
        {
            var updated = await _service.UpdateAsync(id, dto);
            return Ok(updated);
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult<bool>> Deactivate(int id)
        {
            var isActive = await _service.DeactivateAsync(id);
            return Ok(isActive);
        }

        [HttpPatch("{id:int}/reactivate")]
        public async Task<ActionResult<bool>> Reactivate(int id)
        {
            var isActive = await _service.ReactivateAsync(id);
            return Ok(isActive);
        }

        // --- Movements ---

        [HttpGet("{semenSampleId:int}/movements")]
        public async Task<ActionResult<IEnumerable<SemenSampleMovementListItemDto>>> GetMovements(
            int semenSampleId,
            [FromQuery] SemenSampleMovementFilterDto filter)
        {
            var movements = await _movementService.GetBySemenSampleIdAsync(semenSampleId, filter);
            return Ok(movements);
        }

        [HttpGet("{semenSampleId:int}/movements/{movementId:int}")]
        public async Task<ActionResult<SemenSampleMovementDto>> GetMovementById(int semenSampleId, int movementId)
        {
            var movement = await _movementService.GetByIdAsync(semenSampleId, movementId);
            return Ok(movement);
        }

        [HttpPost("{semenSampleId:int}/movements")]
        public async Task<ActionResult<SemenSampleMovementDto>> CreateMovement(
            int semenSampleId,
            [FromBody] SemenSampleMovementCreateDto dto)
        {
            var created = await _movementService.CreateAsync(semenSampleId, dto);
            return CreatedAtAction(
                nameof(GetMovementById),
                new { semenSampleId, movementId = created.Id },
                created);
        }

        [HttpPatch("{semenSampleId:int}/movements/{movementId:int}")]
        public async Task<ActionResult<SemenSampleMovementDto>> UpdateMovement(
            int semenSampleId,
            int movementId,
            [FromBody] SemenSampleMovementUpdateDto dto)
        {
            var updated = await _movementService.UpdateAsync(semenSampleId, movementId, dto);
            return Ok(updated);
        }

        [HttpDelete("{semenSampleId:int}/movements/{movementId:int}")]
        public async Task<IActionResult> DeactivateMovement(int semenSampleId, int movementId)
        {
            await _movementService.DeactivateAsync(semenSampleId, movementId);
            return NoContent();
        }
    }
}
