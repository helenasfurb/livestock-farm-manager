using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Interfaces;

namespace MuuBoi.Api.Controllers
{
    [ApiController]
    [Route("api/vaccines")]
    [Authorize]
    public class VaccinesController : ControllerBase
    {
        private readonly IVaccineService _vaccineService;

        public VaccinesController(IVaccineService vaccineService)
        {
            _vaccineService = vaccineService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<VaccineDto>>> GetAll([FromQuery] VaccineFilterDto filter)
        {
            var vaccines = await _vaccineService.GetAllVaccinesAsync(filter);
            return Ok(vaccines);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<VaccineDto>> GetById(int id)
        {
            var vaccine = await _vaccineService.GetVaccineByIdAsync(id);
            return Ok(vaccine);
        }

        [HttpPost]
        public async Task<ActionResult<VaccineDto>> Create([FromBody] VaccineCreateDto dto)
        {
            var created = await _vaccineService.CreateVaccineAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPatch("{id:int}")]
        public async Task<ActionResult<VaccineDto>> Update(int id, [FromBody] VaccineUpdateDto dto)
        {
            var updated = await _vaccineService.UpdateVaccineAsync(id, dto);
            return Ok(updated);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _vaccineService.DeleteVaccineAsync(id);
            return NoContent();
        }
    }
}
