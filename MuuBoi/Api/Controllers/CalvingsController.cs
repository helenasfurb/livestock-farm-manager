using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Helpers;
using MuuBoi.Application.Interfaces;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Api.Controllers
{
    [ApiController]
    [Authorize]
    public class CalvingsController : ControllerBase
    {
        private readonly IAnimalCalvingService _service;

        public CalvingsController(IAnimalCalvingService service)
        {
            _service = service;
        }

        [HttpGet("api/calvings/calf-vital-statuses")]
        public ActionResult<IEnumerable<LookupDto>> GetCalfVitalStatuses()
            => Ok(EnumHelper.ToLookup<CalfVitalStatus>());

        [HttpPost("api/pregnancies/{pregnancyId:int}/calvings")]
        public async Task<ActionResult<AnimalCalvingDto>> Create(int pregnancyId, [FromBody] AnimalCalvingCreateDto dto)
        {
            var created = await _service.CreateAsync(pregnancyId, dto);
            return CreatedAtAction("GetById", "Pregnancies", new { id = pregnancyId }, created);
        }

        [HttpDelete("api/calvings/{id:int}")]
        public async Task<IActionResult> Inactivate(int id)
        {
            await _service.InactivateAsync(id);
            return NoContent();
        }
    }
}
