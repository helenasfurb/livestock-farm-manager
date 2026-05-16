using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Interfaces;
using MuuBoi.DTOs;

namespace MuuBoi.Api.Controllers
{
    [ApiController]
    [Route("api/animals/{animalId}/vaccinations")]
    [Authorize]
    public class AnimalVaccinationsController : ControllerBase
    {
        private readonly IAnimalVaccinationService _animalVaccinationService;

        public AnimalVaccinationsController(IAnimalVaccinationService animalVaccinationService)
        {
            _animalVaccinationService = animalVaccinationService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<AnimalVaccinationDto>>> GetAll(string animalId)
        {
            var vaccinations = await _animalVaccinationService.GetAllAnimalVaccinationsAsync(animalId);
            return Ok(vaccinations);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<AnimalVaccinationDto>> GetById(string animalId, int id)
        {
            var vaccination = await _animalVaccinationService.GetAnimalVaccinationByIdAsync(id, animalId);
            if (vaccination == null) return NotFound();
            return Ok(vaccination);
        }

        [HttpPost]
        public async Task<ActionResult<AnimalVaccinationDto>> Create(string animalId, [FromBody] AnimalVaccinationCreateDto dto)
        {
            var created = await _animalVaccinationService.CreateAnimalVaccinationAsync(dto, animalId);
            return CreatedAtAction(nameof(GetById), new { animalId, id = created.Id }, created);
        }

        [HttpPatch("{id:int}")]
        public async Task<ActionResult<AnimalVaccinationDto>> Update(string animalId, int id, [FromBody] AnimalVaccinationUpdateDto dto)
        {
            var updated = await _animalVaccinationService.UpdateAnimalVaccinationAsync(id, animalId, dto);
            if (updated == null) return NotFound();
            return Ok(updated);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(string animalId, int id)
        {
            var deleted = await _animalVaccinationService.DeleteAnimalVaccinationAsync(id, animalId);
            if (deleted == null) return NotFound();
            return NoContent();
        }
    }
}
