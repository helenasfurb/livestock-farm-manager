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
    public class LactationsController : ControllerBase
    {
        private readonly ILactationService _service;

        public LactationsController(ILactationService service)
        {
            _service = service;
        }

        [HttpGet("api/lactations/origins")]
        public ActionResult<IEnumerable<LookupDto>> GetOrigins()
            => Ok(EnumHelper.ToLookup<LactationOrigin>());

        [HttpGet("api/animals/{animalId:int}/lactations")]
        public async Task<ActionResult<IEnumerable<LactationListItemDto>>> GetByAnimal(int animalId)
        {
            var lactations = await _service.GetByAnimalIdAsync(animalId);
            return Ok(lactations);
        }

        [HttpGet("api/animals/{animalId:int}/lactations/current")]
        public async Task<ActionResult<LactationDto>> GetCurrent(int animalId)
        {
            var current = await _service.GetCurrentByAnimalIdAsync(animalId);
            return current == null ? NoContent() : Ok(current);
        }

        [HttpPost("api/animals/{animalId:int}/lactations")]
        public async Task<ActionResult<LactationDto>> Create(int animalId, [FromBody] LactationCreateDto dto)
        {
            var created = await _service.CreateAsync(animalId, dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpGet("api/lactations/{id:int}")]
        public async Task<ActionResult<LactationDto>> GetById(int id)
        {
            var lactation = await _service.GetByIdAsync(id);
            return Ok(lactation);
        }

        [HttpPatch("api/lactations/{id:int}")]
        public async Task<ActionResult<LactationDto>> Update(int id, [FromBody] LactationUpdateDto dto)
        {
            var updated = await _service.UpdateAsync(id, dto);
            return Ok(updated);
        }

        [HttpPost("api/lactations/{id:int}/dry-off")]
        public async Task<ActionResult<LactationDto>> DryOff(int id, [FromBody] LactationDryOffDto dto)
        {
            var updated = await _service.DryOffAsync(id, dto);
            return Ok(updated);
        }

        [HttpDelete("api/lactations/{id:int}/dry-off")]
        public async Task<ActionResult<LactationDto>> UndoDryOff(int id)
        {
            var updated = await _service.UndoDryOffAsync(id);
            return Ok(updated);
        }

        [HttpDelete("api/lactations/{id:int}")]
        public async Task<IActionResult> Deactivate(int id)
        {
            await _service.DeactivateAsync(id);
            return NoContent();
        }
    }
}
