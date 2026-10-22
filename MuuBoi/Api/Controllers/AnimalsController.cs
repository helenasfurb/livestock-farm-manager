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
        private readonly IVaccinationEventService _vaccinationEventService;
        private readonly IHealthCaseService _healthCaseService;
        private readonly IAnimalPhotoService _animalPhotoService;

        public AnimalsController(
            IAnimalService animalService,
            IVaccinationEventService vaccinationEventService,
            IHealthCaseService healthCaseService,
            IAnimalPhotoService animalPhotoService)
        {
            _animalService = animalService;
            _vaccinationEventService = vaccinationEventService;
            _healthCaseService = healthCaseService;
            _animalPhotoService = animalPhotoService;
        }

        [HttpGet("{id:int}/vaccination-history")]
        public async Task<ActionResult<IEnumerable<VaccinationHistoryItemDto>>> GetVaccinationHistory(int id)
        {
            var history = await _vaccinationEventService.GetAnimalHistoryAsync(id);
            return Ok(history);
        }

        [HttpGet("{id:int}/health-history")]
        public async Task<ActionResult<IEnumerable<HealthCaseListItemDto>>> GetHealthHistory(int id)
        {
            var history = await _healthCaseService.GetAnimalHistoryAsync(id);
            return Ok(history);
        }

        [HttpGet("sanitary-statuses")]
        public ActionResult<IEnumerable<LookupDto>> GetSanitaryStatuses() => Ok(EnumHelper.ToLookup<SanitaryStatus>());

        [HttpGet("genders")]
        public ActionResult<IEnumerable<LookupDto>> GetGenders() => Ok(EnumHelper.ToLookup<AnimalGender>());

        [HttpGet("breeds")]
        public ActionResult<IEnumerable<LookupDto>> GetBreeds() => Ok(EnumHelper.ToLookup<AnimalBreed>());

        [HttpGet("classifications")]
        public ActionResult<IEnumerable<LookupDto>> GetClassifications() => Ok(EnumHelper.ToLookup<AnimalClassification>());

        [HttpGet("purposes")]
        public ActionResult<IEnumerable<LookupDto>> GetPurposes() => Ok(EnumHelper.ToLookup<AnimalPurpose>());

        [HttpGet("origins")]
        public ActionResult<IEnumerable<LookupDto>> GetOrigins() => Ok(EnumHelper.ToLookup<AnimalOrigin>());

        [HttpGet("exit-reasons")]
        public ActionResult<IEnumerable<LookupDto>> GetExitReasons() => Ok(EnumHelper.ToLookup<AnimalExitReason>());

        [HttpGet("body-condition-scores")]
        public ActionResult<IEnumerable<LookupDto>> GetBodyConditionScores() => Ok(EnumHelper.ToLookup<BodyConditionScore>());

        [HttpGet("reproductive-statuses")]
        public ActionResult<IEnumerable<LookupDto>> GetReproductiveStatuses() => Ok(EnumHelper.ToLookup<ReproductiveStatus>());

        [HttpGet]
        public async Task<ActionResult<IEnumerable<AnimalListItemDto>>> GetAll([FromQuery] AnimalFilterDto filter)
        {
            var animals = await _animalService.GetAllAnimalsAsync(filter);
            return Ok(animals);
        }

        [HttpGet("changes")]
        public async Task<ActionResult<SyncPageDto<AnimalSyncDto>>> GetChanges([FromQuery] string? since, [FromQuery] int? limit)
        {
            var page = await _animalService.GetChangesAsync(since, limit);
            return Ok(page);
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

        [HttpPut("{id:int}/photo")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(3 * 1024 * 1024)]
        public async Task<ActionResult<AnimalPhotoDto>> UploadPhoto(int id, [FromForm] AnimalPhotoUploadDto dto)
        {
            var result = await _animalPhotoService.UploadPhotoAsync(id, dto);
            return Ok(result);
        }

        [HttpGet("{id:int}/photo")]
        public async Task<IActionResult> GetPhoto(int id)
        {
            var url = await _animalPhotoService.GetPhotoReadUrlAsync(id, thumbnail: false);
            return Redirect(url.ToString());
        }

        [HttpGet("{id:int}/photo/thumbnail")]
        public async Task<IActionResult> GetThumbnail(int id)
        {
            var url = await _animalPhotoService.GetPhotoReadUrlAsync(id, thumbnail: true);
            return Redirect(url.ToString());
        }

        [HttpDelete("{id:int}/photo")]
        public async Task<IActionResult> DeletePhoto(int id, [FromQuery] DateTime? updatedAt)
        {
            await _animalPhotoService.DeletePhotoAsync(id, updatedAt);
            return NoContent();
        }

        [HttpGet("{id:int}/exit-records")]
        public async Task<ActionResult<IEnumerable<AnimalExitRecordDto>>> GetExitRecords(int id)
        {
            var records = await _animalService.GetExitRecordsAsync(id);
            return Ok(records);
        }
    }
}
