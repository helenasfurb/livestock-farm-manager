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

        [HttpPost]
        public async Task<IActionResult> Create(string animalId, [FromBody] WeightRecordCreateDto dto)
        {
            var created = await _weightRecordService.CreateWeightRecordAsync(dto, animalId);
            return CreatedAtAction(nameof(GetById), new { weightRecordId = created.Id, animalId }, created);
        }

        [HttpGet]
        public async Task<ActionResult<WeightRecordDto>> GetById(string weightRecordId, string animalId)
        {

            var weightRecord = await _weightRecordService.GetWeightRecordByIdAsync(int.Parse(weightRecordId), animalId);
            return Ok(weightRecord);
        }
    }
}
