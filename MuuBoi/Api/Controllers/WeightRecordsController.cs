using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Interfaces;

namespace MuuBoi.Api.Controllers
{
    [ApiController]
    [Route("api/animals/{animalId:int}/weight-records")]
    [Authorize]
    public class WeightRecordsController : ControllerBase
    {
        private readonly IWeightRecordService _weightRecordService;
        private readonly ICurrentUserService _currentUserService;

        public WeightRecordsController(IWeightRecordService weightRecordService, ICurrentUserService currentUserService)
        {
            _weightRecordService = weightRecordService;
            _currentUserService = currentUserService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<WeightRecordDto>>> GetAll(int animalId)
        {
            var weightRecords = await _weightRecordService.GetAllWeightRecordsAsync(animalId);
            return Ok(weightRecords);
        }

        [HttpGet("~/api/animals/weight-records/changes")]
        public async Task<ActionResult<SyncPageDto<WeightRecordDto>>> GetChanges([FromQuery] string? since, [FromQuery] int? limit)
        {
            var page = await _weightRecordService.GetChangesAsync(since, limit);
            return Ok(page);
        }

        [HttpGet("{weightRecordId:int}")]
        public async Task<ActionResult<WeightRecordDto>> GetById(int animalId, int weightRecordId)
        {
            var weightRecord = await _weightRecordService.GetWeightRecordByIdAsync(weightRecordId, animalId);
            return Ok(weightRecord);
        }

        [HttpPost]
        public async Task<IActionResult> Create(int animalId, [FromBody] WeightRecordCreateDto dto)
        {
            var created = await _weightRecordService.CreateWeightRecordAsync(dto, animalId);
            return CreatedAtAction(nameof(GetById), new { animalId, weightRecordId = created.Id }, created);
        }

        [HttpDelete("{weightRecordId:int}")]
        public async Task<IActionResult> Delete(int animalId, int weightRecordId)
        {
            await _weightRecordService.DeleteWeightRecordAsync(weightRecordId, animalId);
            return NoContent();
        }

        [HttpPatch("{weightRecordId:int}")]
        public async Task<ActionResult<WeightRecordDto>> Update(int animalId, int weightRecordId, [FromBody] WeightRecordUpdateDto dto)
        {
            var updatedRecord = await _weightRecordService.UpdateWeightRecordAsync(weightRecordId, animalId, dto);
            return Ok(updatedRecord);
        }
    }
}
