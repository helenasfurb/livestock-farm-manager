using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MuuBoi.DTOs;
using MuuBoi.Interfaces;

namespace MuuBoi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AnimalsController : ControllerBase
    {
        private readonly IAnimalService _animalService;
        private readonly ICurrentUserService _currentUserService;

        public AnimalsController(IAnimalService animalService, ICurrentUserService currentUserService)
        {
            _animalService = animalService;
            _currentUserService = currentUserService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<AnimalDto>>> GetAll()
        {
            var animals = await _animalService.GetAllAnimalsAsync(_currentUserService.UserId);
            return Ok(animals);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<AnimalDto>> GetById(int id)
        {
            var animal = await _animalService.GetAnimalByIdAsync(id, _currentUserService.UserId);
            if (animal == null)
                return NotFound();

            return Ok(animal);
        }

        [HttpPost]
        public async Task<ActionResult<AnimalDto>> Create([FromBody] AnimalCreateDto dto)
        {
            var created = await _animalService.CreateAnimalAsync(dto, _currentUserService.UserId);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _animalService.DeleteAnimalAsync(id, _currentUserService.UserId);
            if (deleted == null)
                return NotFound();

            return NoContent();
        }
    }
}
