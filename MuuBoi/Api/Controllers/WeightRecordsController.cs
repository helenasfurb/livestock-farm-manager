using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Interfaces;
using MuuBoi.DTOs;
using MuuBoi.Interfaces;

namespace MuuBoi.Api.Controllers
{
    [ApiController]
    [Route("api/animals/{animalId}/weight-records")]
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
        public async Task<ActionResult<IEnumerable<WeightRecordDto>>> GetAll(string animalId)
        {
            var weightRecords = await _weightRecordService.GetAllWeightRecordsAsync(animalId);
            return Ok(weightRecords);
        }

        [HttpGet("{weightRecordId}")]
        public async Task<ActionResult<WeightRecordDto>> GetById(string animalId, string weightRecordId)
        {
            var weightRecord = await _weightRecordService.GetWeightRecordByIdAsync(int.Parse(weightRecordId), animalId);
            if (weightRecord == null) return NotFound();
            return Ok(weightRecord);
        }

        [HttpPost]
        public async Task<IActionResult> Create(string animalId, [FromBody] WeightRecordCreateDto dto)
        {
            var created = await _weightRecordService.CreateWeightRecordAsync(dto, animalId);
            return CreatedAtAction(nameof(GetById), new { animalId, weightRecordId = created.Id }, created);
        }

        [HttpDelete("{weightRecordId}")]
        public async Task<IActionResult> Delete(string animalId, string weightRecordId)
        {
            var deleted = await _weightRecordService.DeleteWeightRecordAsync(int.Parse(weightRecordId), animalId);
            if (deleted == null) return NotFound();
            return NoContent();
        }

        [HttpPatch("{weightRecordId}")]
        public async Task<ActionResult<WeightRecordDto>> Update(string animalId, string weightRecordId, [FromBody] WeightRecordUpdateDto dto)
        {
            var updatedRecord = await _weightRecordService.UpdateWeightRecordAsync(int.Parse(weightRecordId), animalId, dto);
            if (updatedRecord == null) return NotFound();
     
            return Ok(updatedRecord);
        }
    }
}
