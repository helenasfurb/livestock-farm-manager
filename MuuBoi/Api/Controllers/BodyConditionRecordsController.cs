using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Interfaces;

namespace MuuBoi.Api.Controllers
{
    [ApiController]
    [Route("api/animals/{animalId:int}/body-condition-records")]
    [Authorize]
    public class BodyConditionRecordsController : ControllerBase
    {
        private readonly IBodyConditionRecordService _service;

        public BodyConditionRecordsController(IBodyConditionRecordService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<BodyConditionRecordDto>>> GetAll(int animalId)
        {
            var records = await _service.GetByAnimalIdAsync(animalId);
            return Ok(records);
        }

        [HttpPost]
        public async Task<ActionResult<BodyConditionRecordDto>> Create(int animalId, [FromBody] BodyConditionRecordCreateDto dto)
        {
            var created = await _service.CreateAsync(animalId, dto);
            return CreatedAtAction(nameof(GetAll), new { animalId }, created);
        }

        [HttpPatch("{recordId:int}")]
        public async Task<ActionResult<BodyConditionRecordDto>> Update(int animalId, int recordId, [FromBody] BodyConditionRecordUpdateDto dto)
        {
            var updated = await _service.UpdateAsync(animalId, recordId, dto);
            return Ok(updated);
        }
    }
}
