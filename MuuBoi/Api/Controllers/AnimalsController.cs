using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MuuBoi.Application.Helpers;
using MuuBoi.DTOs;
using MuuBoi.Enums;
using MuuBoi.Interfaces;

namespace MuuBoi.Api.Controllers
{
    [ApiController]
    [Route("api/animals")]
    [Authorize]
    public class AnimalsController : ControllerBase
    {
        private readonly IAnimalService _animalService;

        public AnimalsController(IAnimalService animalService)
        {
            _animalService = animalService;
        }

        [HttpGet("genders")]
        public IActionResult GetGenders()
        {
            return Ok(EnumHelper.ToLookup<AnimalGender>());
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<AnimalDto>>> GetAll()
        {
            var animals = await _animalService.GetAllAnimalsAsync();
            return Ok(animals);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<AnimalDto>> GetById(int id)
        {
            var animal = await _animalService.GetAnimalByIdAsync(id);
            if (animal == null) return NotFound();
            return Ok(animal);
        }

        [HttpPost]
        public async Task<ActionResult<AnimalDto>> Create([FromBody] AnimalCreateDto dto)
        {
            var created = await _animalService.CreateAnimalAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _animalService.DeleteAnimalAsync(id);
            if (deleted == null) return NotFound();
            return NoContent();
        }

        [HttpPatch("{id:int}")]
        public async Task<ActionResult<AnimalDto>> Update(int id, [FromBody] AnimalUpdateDto dto)
        {
            var updated = await _animalService.UpdateAnimalAsync(id, dto);
            if (updated == null) return NotFound();
            return Ok(updated);
        }
    }
}
