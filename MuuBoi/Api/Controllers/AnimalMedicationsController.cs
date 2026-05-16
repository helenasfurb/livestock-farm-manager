using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Interfaces;
using MuuBoi.DTOs;

namespace MuuBoi.Api.Controllers
{
    [ApiController]
    [Route("api/animals/{animalId}/medications")]
    [Authorize]
    public class AnimalMedicationsController : ControllerBase
    {
        private readonly IAnimalMedicationService _animalMedicationService;

        public AnimalMedicationsController(IAnimalMedicationService animalMedicationService)
        {
            _animalMedicationService = animalMedicationService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<AnimalMedicationDto>>> GetAll(string animalId)
        {
            var medications = await _animalMedicationService.GetAllAnimalMedicationsAsync(animalId);
            return Ok(medications);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<AnimalMedicationDto>> GetById(string animalId, int id)
        {
            var medication = await _animalMedicationService.GetAnimalMedicationByIdAsync(id, animalId);
            if (medication == null) return NotFound();
            return Ok(medication);
        }

        [HttpPost]
        public async Task<ActionResult<AnimalMedicationDto>> Create(string animalId, [FromBody] AnimalMedicationCreateDto dto)
        {
            var created = await _animalMedicationService.CreateAnimalMedicationAsync(dto, animalId);
            return CreatedAtAction(nameof(GetById), new { animalId, id = created.Id }, created);
        }

        [HttpPatch("{id:int}")]
        public async Task<ActionResult<AnimalMedicationDto>> Update(string animalId, int id, [FromBody] AnimalMedicationUpdateDto dto)
        {
            var updated = await _animalMedicationService.UpdateAnimalMedicationAsync(id, animalId, dto);
            if (updated == null) return NotFound();
            return Ok(updated);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(string animalId, int id)
        {
            var deleted = await _animalMedicationService.DeleteAnimalMedicationAsync(id, animalId);
            if (deleted == null) return NotFound();
            return NoContent();
        }
    }
}
