using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Helpers;
using MuuBoi.Application.Interfaces;
using MuuBoi.Domain.Enums;

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
        public IActionResult GetGenders() => Ok(EnumHelper.ToLookup<AnimalGender>());

        [HttpGet("breeds")]
        public IActionResult GetBreeds() => Ok(EnumHelper.ToLookup<AnimalBreed>());

        [HttpGet("classifications")]
        public IActionResult GetClassifications() => Ok(EnumHelper.ToLookup<AnimalClassification>());

        [HttpGet("purposes")]
        public IActionResult GetPurposes() => Ok(EnumHelper.ToLookup<AnimalPurpose>());

        [HttpGet("origins")]
        public IActionResult GetOrigins() => Ok(EnumHelper.ToLookup<AnimalOrigin>());

        [HttpGet("exit-reasons")]
        public IActionResult GetExitReasons() => Ok(EnumHelper.ToLookup<AnimalExitReason>());

        [HttpGet("death-causes")]
        public IActionResult GetDeathCauses() => Ok(EnumHelper.ToLookup<AnimalDeathCause>());

        [HttpGet]
        public async Task<ActionResult<IEnumerable<AnimalListItemDto>>> GetAll([FromQuery] AnimalFilterDto filter)
        {
            var animals = await _animalService.GetAllAnimalsAsync(filter);
            return Ok(animals);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<AnimalDto>> GetById(int id)
        {
            var animal = await _animalService.GetAnimalByIdAsync(id);
            return Ok(animal);
        }

        [HttpPost]
        public async Task<ActionResult<AnimalDto>> Create([FromBody] AnimalCreateDto dto)
        {
            var created = await _animalService.CreateAnimalAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPatch("{id:int}")]
        public async Task<ActionResult<AnimalDto>> Update(int id, [FromBody] AnimalUpdateDto dto)
        {
            var updated = await _animalService.UpdateAnimalAsync(id, dto);
            return Ok(updated);
        }

        [HttpPatch("{id:int}/exit")]
        public async Task<ActionResult<AnimalDto>> Exit(int id, [FromBody] AnimalExitDto dto)
        {
            var result = await _animalService.ExitAnimalAsync(id, dto);
            return Ok(result);
        }

        [HttpPatch("{id:int}/reactivate")]
        public async Task<ActionResult<AnimalDto>> Reactivate(int id)
        {
            var result = await _animalService.ReactivateAnimalAsync(id);
            return Ok(result);
        }
    }
}
