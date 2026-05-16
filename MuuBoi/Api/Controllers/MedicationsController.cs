using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Interfaces;
using MuuBoi.DTOs;
using MuuBoi.Interfaces;

namespace MuuBoi.Api.Controllers
{
    [ApiController]
    [Route("api/medications")]
    [Authorize]
    public class MedicationsController : ControllerBase
    {
        private readonly IMedicationService _medicationService;
        private readonly ICurrentUserService _currentUserService;

        public MedicationsController(IMedicationService medicationService, ICurrentUserService currentUserService)
        {
            _medicationService = medicationService;
            _currentUserService = currentUserService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<MedicationDto>>> GetAll()
        {
            var medications = await _medicationService.GetAllMedicationsAsync(_currentUserService.UserId);
            return Ok(medications);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<MedicationDto>> GetById(int id)
        {
            var medication = await _medicationService.GetMedicationByIdAsync(id, _currentUserService.UserId);
            if (medication == null) return NotFound();
            return Ok(medication);
        }

        [HttpPost]
        public async Task<ActionResult<MedicationDto>> Create([FromBody] MedicationCreateDto dto)
        {
            var created = await _medicationService.CreateMedicationAsync(dto, _currentUserService.UserId);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPatch("{id:int}")]
        public async Task<ActionResult<MedicationDto>> Update(int id, [FromBody] MedicationUpdateDto dto)
        {
            var updated = await _medicationService.UpdateMedicationAsync(id, dto, _currentUserService.UserId);
            if (updated == null) return NotFound();
            return Ok(updated);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _medicationService.DeleteMedicationAsync(id, _currentUserService.UserId);
            if (deleted == null) return NotFound();
            return NoContent();
        }
    }
}
