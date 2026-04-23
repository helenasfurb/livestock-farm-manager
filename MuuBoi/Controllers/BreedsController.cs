using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MuuBoi.DTOs;
using MuuBoi.Interfaces;

namespace MuuBoi.Controllers
{
    [ApiController]
    [Route("api/breeds")]
    [Authorize]
    public class BreedsController : ControllerBase
    {
        private readonly IBreedService _breedService;
        private readonly ICurrentUserService _currentUserService;

        public BreedsController(IBreedService breedService, ICurrentUserService currentUserService)
        {
            _breedService = breedService;
            _currentUserService = currentUserService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<BreedDto>>> GetAll()    
        {
            var breeds = await _breedService.GetAllBreedsAsync(_currentUserService.UserId);
            return Ok(breeds);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<BreedDto>> GetById(int id)
        {
            var breed = await _breedService.GetBreedByIdAsync(id, _currentUserService.UserId);
            if (breed  == null)
                return NotFound();

            return Ok(breed);
        }

        [HttpPost]
        public async Task<ActionResult<BreedDto>> Create([FromBody] BreedCreateDto dto)
        {
            var created = await _breedService.CreateBreedAsync(dto, _currentUserService.UserId);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _breedService.DeleteBreedAsync(id, _currentUserService.UserId);
            if (deleted == null)
                return NotFound();

            return NoContent();
        }

        [HttpPatch("{id:int}")]

        public async Task<ActionResult<BreedDto>> Update(int id, [FromBody] BreedUpdateDto dto)
        {
            var updated = await _breedService.UpdateBreedAsync(id,dto, _currentUserService.UserId);
            if (updated == null)
                return NotFound();

            return Ok(updated);
        }
    }
}
