using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Interfaces;

namespace MuuBoi.Api.Controllers
{
    [ApiController]
    [Route("api/medications")]
    [Authorize]
    public class MedicationsController : ControllerBase
    {
        private readonly IMedicationService _medicationService;

        public MedicationsController(IMedicationService medicationService)
        {
            _medicationService = medicationService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<MedicationDto>>> GetAll()
        {
            var medications = await _medicationService.GetAllMedicationsAsync();
            return Ok(medications);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<MedicationDto>> GetById(int id)
        {
            var medication = await _medicationService.GetMedicationByIdAsync(id);
            return Ok(medication);
        }

        [HttpPost]
        public async Task<ActionResult<MedicationDto>> Create([FromBody] MedicationCreateDto dto)
        {
            var created = await _medicationService.CreateMedicationAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPatch("{id:int}")]
        public async Task<ActionResult<MedicationDto>> Update(int id, [FromBody] MedicationUpdateDto dto)
        {
            var updated = await _medicationService.UpdateMedicationAsync(id, dto);
            return Ok(updated);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _medicationService.DeleteMedicationAsync(id);
            return NoContent();
        }
    }
}
