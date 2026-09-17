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
    public class PregnanciesController : ControllerBase
    {
        private readonly IAnimalPregnancyService _service;

        public PregnanciesController(IAnimalPregnancyService service)
        {
            _service = service;
        }

        [HttpGet("api/pregnancies/statuses")]
        public ActionResult<IEnumerable<LookupDto>> GetStatuses()
            => Ok(EnumHelper.ToLookup<AnimalPregnancyStatus>());

        [HttpGet("api/pregnancies")]
        public async Task<ActionResult<IEnumerable<AnimalPregnancyListItemDto>>> GetAll([FromQuery] AnimalPregnancyFilterDto filter)
        {
            var pregnancies = await _service.GetAllAsync(filter);
            return Ok(pregnancies);
        }

        [HttpGet("api/animals/{animalId:int}/pregnancies")]
        public async Task<ActionResult<IEnumerable<AnimalPregnancyListItemDto>>> GetByAnimal(int animalId, [FromQuery] bool? isActive)
        {
            var pregnancies = await _service.GetByAnimalIdAsync(animalId, isActive);
            return Ok(pregnancies);
        }

        [HttpGet("api/pregnancies/{id:int}")]
        public async Task<ActionResult<AnimalPregnancyDto>> GetById(int id)
        {
            var pregnancy = await _service.GetByIdAsync(id);
            return Ok(pregnancy);
        }

        [HttpPost("api/animals/{animalId:int}/pregnancies")]
        public async Task<ActionResult<AnimalPregnancyDto>> CreateRetroactive(int animalId, [FromBody] AnimalPregnancyRetroactiveCreateDto dto)
        {
            var created = await _service.CreateRetroactiveAsync(animalId, dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPatch("api/pregnancies/{id:int}/status")]
        public async Task<ActionResult<AnimalPregnancyDto>> RegisterLoss(int id, [FromBody] AnimalPregnancyStatusUpdateDto dto)
        {
            var updated = await _service.RegisterLossAsync(id, dto);
            return Ok(updated);
        }

        [HttpDelete("api/pregnancies/{id:int}")]
        public async Task<IActionResult> Inactivate(int id)
        {
            await _service.InactivateAsync(id);
            return NoContent();
        }
    }
}
