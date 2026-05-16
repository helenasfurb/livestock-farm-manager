using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Interfaces;
using MuuBoi.DTOs;
using MuuBoi.Interfaces;

namespace MuuBoi.Api.Controllers
{
    [ApiController]
    [Route("api/vaccines")]
    [Authorize]
    public class VaccinesController : ControllerBase
    {
        private readonly IVaccineService _vaccineService;
        private readonly ICurrentUserService _currentUserService;

        public VaccinesController(IVaccineService vaccineService, ICurrentUserService currentUserService)
        {
            _vaccineService = vaccineService;
            _currentUserService = currentUserService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<VaccineDto>>> GetAll()
        {
            var vaccines = await _vaccineService.GetAllVaccinesAsync(_currentUserService.UserId);
            return Ok(vaccines);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<VaccineDto>> GetById(int id)
        {
            var vaccine = await _vaccineService.GetVaccineByIdAsync(id, _currentUserService.UserId);
            if (vaccine == null) return NotFound();
            return Ok(vaccine);
        }

        [HttpPost]
        public async Task<ActionResult<VaccineDto>> Create([FromBody] VaccineCreateDto dto)
        {
            var created = await _vaccineService.CreateVaccineAsync(dto, _currentUserService.UserId);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPatch("{id:int}")]
        public async Task<ActionResult<VaccineDto>> Update(int id, [FromBody] VaccineUpdateDto dto)
        {
            var updated = await _vaccineService.UpdateVaccineAsync(id, dto, _currentUserService.UserId);
            if (updated == null) return NotFound();
            return Ok(updated);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _vaccineService.DeleteVaccineAsync(id, _currentUserService.UserId);
            if (deleted == null) return NotFound();
            return NoContent();
        }
    }
}
