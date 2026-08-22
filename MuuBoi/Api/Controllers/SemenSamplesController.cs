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

        public SemenSamplesController(ISemenSampleService service)
        {
            _service = service;
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
        public async Task<IActionResult> Deactivate(int id)
        {
            await _service.DeactivateAsync(id);
            return NoContent();
        }
    }
}
