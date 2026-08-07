using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MuuBoi.DTOs;
using MuuBoi.Interfaces;

namespace MuuBoi.Api.Controllers
{
    [ApiController]
    [Route("api/breeds")]
    [Authorize]
    public class BreedsController : ControllerBase
    {
        private readonly IBreedService _breedService;

        public BreedsController(IBreedService breedService)
        {
            _breedService = breedService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<BreedDto>>> GetAll()
        {
            var breeds = await _breedService.GetAllBreedsAsync();
            return Ok(breeds);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<BreedDto>> GetById(int id)
        {
            var breed = await _breedService.GetBreedByIdAsync(id);
            if (breed == null) return NotFound();
            return Ok(breed);
        }

        [HttpPost]
        public async Task<ActionResult<BreedDto>> Create([FromBody] BreedCreateDto dto)
        {
            var created = await _breedService.CreateBreedAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _breedService.DeleteBreedAsync(id);
            if (deleted == null) return NotFound();
            return NoContent();
        }

        [HttpPatch("{id:int}")]
        public async Task<ActionResult<BreedDto>> Update(int id, [FromBody] BreedUpdateDto dto)
        {
            var updated = await _breedService.UpdateBreedAsync(id, dto);
            if (updated == null) return NotFound();
            return Ok(updated);
        }
    }
}
