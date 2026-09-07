using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Helpers;
using MuuBoi.Application.Interfaces;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Api.Controllers
{
    [ApiController]
    [Route("api/health-cases")]
    [Authorize]
    public class HealthCasesController : ControllerBase
    {
        private readonly IHealthCaseService _service;

        public HealthCasesController(IHealthCaseService service)
        {
            _service = service;
        }

        [HttpGet("disease-types")]
        public ActionResult<IEnumerable<LookupDto>> GetDiseaseTypes()
            => Ok(EnumHelper.ToLookup<DiseaseType>());

        [HttpGet("quarters")]
        public ActionResult<IEnumerable<LookupDto>> GetQuarters()
            => Ok(EnumHelper.ToLookup<Quarter>());

        [HttpGet("test-types")]
        public ActionResult<IEnumerable<LookupDto>> GetTestTypes()
            => Ok(EnumHelper.ToLookup<MastitisTestType>());

        [HttpGet("statuses")]
        public ActionResult<IEnumerable<LookupDto>> GetStatuses()
            => Ok(EnumHelper.ToLookup<HealthCaseStatus>());

        [HttpGet]
        public async Task<ActionResult<IEnumerable<HealthCaseListItemDto>>> GetAll([FromQuery] HealthCaseFilterDto filter)
        {
            var cases = await _service.GetAllAsync(filter);
            return Ok(cases);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<HealthCaseDto>> GetById(int id)
        {
            var healthCase = await _service.GetByIdAsync(id);
            return Ok(healthCase);
        }

        [HttpPost]
        public async Task<ActionResult<HealthCaseDto>> Create([FromBody] HealthCaseCreateDto dto)
        {
            var created = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPatch("{id:int}")]
        public async Task<ActionResult<HealthCaseDto>> Update(int id, [FromBody] HealthCaseUpdateDto dto)
        {
            var updated = await _service.UpdateAsync(id, dto);
            return Ok(updated);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Deactivate(int id)
        {
            await _service.DeactivateAsync(id);
            return NoContent();
        }

        [HttpPost("{id:int}/medications")]
        public async Task<ActionResult<MedicationUseDto>> AddMedication(int id, [FromBody] MedicationUseCreateDto dto)
        {
            var created = await _service.AddMedicationAsync(id, dto);
            return CreatedAtAction(nameof(GetById), new { id }, created);
        }

        [HttpDelete("{id:int}/medications/{medicationId:int}")]
        public async Task<IActionResult> DeactivateMedication(int id, int medicationId)
        {
            await _service.DeactivateMedicationAsync(id, medicationId);
            return NoContent();
        }

        [HttpPost("{id:int}/tests")]
        public async Task<ActionResult<MastitisTestDto>> AddTest(int id, [FromBody] MastitisTestCreateDto dto)
        {
            var created = await _service.AddTestAsync(id, dto);
            return CreatedAtAction(nameof(GetById), new { id }, created);
        }

        [HttpDelete("{id:int}/tests/{testId:int}")]
        public async Task<IActionResult> DeactivateTest(int id, int testId)
        {
            await _service.DeactivateTestAsync(id, testId);
            return NoContent();
        }
    }
}
