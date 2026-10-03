using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Interfaces;

namespace MuuBoi.Api.Controllers
{
    [ApiController]
    [Route("api/animals/{animalId:int}/medications")]
    [Authorize]
    public class AnimalMedicationsController : ControllerBase
    {
        private readonly IAnimalMedicationService _animalMedicationService;

        public AnimalMedicationsController(IAnimalMedicationService animalMedicationService)
        {
            _animalMedicationService = animalMedicationService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<AnimalMedicationDto>>> GetAll(int animalId)
        {
            var medications = await _animalMedicationService.GetAllAnimalMedicationsAsync(animalId);
            return Ok(medications);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<AnimalMedicationDto>> GetById(int animalId, int id)
        {
            var medication = await _animalMedicationService.GetAnimalMedicationByIdAsync(id, animalId);
            return Ok(medication);
        }

        [HttpPost]
        public async Task<ActionResult<AnimalMedicationDto>> Create(int animalId, [FromBody] AnimalMedicationCreateDto dto)
        {
            var created = await _animalMedicationService.CreateAnimalMedicationAsync(dto, animalId);
            return CreatedAtAction(nameof(GetById), new { animalId, id = created.Id }, created);
        }

        [HttpPatch("{id:int}")]
        public async Task<ActionResult<AnimalMedicationDto>> Update(int animalId, int id, [FromBody] AnimalMedicationUpdateDto dto)
        {
            var updated = await _animalMedicationService.UpdateAnimalMedicationAsync(id, animalId, dto);
            return Ok(updated);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int animalId, int id)
        {
            await _animalMedicationService.DeleteAnimalMedicationAsync(id, animalId);
            return NoContent();
        }
    }
}
