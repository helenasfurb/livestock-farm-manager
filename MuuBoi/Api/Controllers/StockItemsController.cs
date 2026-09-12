using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Interfaces;

namespace MuuBoi.Api.Controllers
{
    [ApiController]
    [Route("api/stock-items")]
    [Authorize]
    public class StockItemsController : ControllerBase
    {
        private readonly IStockItemService _service;
        private readonly IStockMovementService _movementService;

        public StockItemsController(IStockItemService service, IStockMovementService movementService)
        {
            _service = service;
            _movementService = movementService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<StockItemListItemDto>>> GetAll([FromQuery] StockItemFilterDto filter)
        {
            var items = await _service.GetAllAsync(filter);
            return Ok(items);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<StockItemDto>> GetById(int id)
        {
            var item = await _service.GetByIdAsync(id);
            return Ok(item);
        }

        [HttpPost]
        public async Task<ActionResult<StockItemDto>> Create([FromBody] StockItemCreateDto dto)
        {
            var created = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPatch("{id:int}")]
        public async Task<ActionResult<StockItemDto>> Update(int id, [FromBody] StockItemUpdateDto dto)
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

        [HttpGet("{stockItemId:int}/movements")]
        public async Task<ActionResult<IEnumerable<StockMovementListItemDto>>> GetMovements(
            int stockItemId,
            [FromQuery] StockMovementFilterDto filter)
        {
            var movements = await _movementService.GetByStockItemIdAsync(stockItemId, filter);
            return Ok(movements);
        }

        [HttpGet("{stockItemId:int}/movements/{movementId:int}")]
        public async Task<ActionResult<StockMovementDto>> GetMovementById(int stockItemId, int movementId)
        {
            var movement = await _movementService.GetByIdAsync(stockItemId, movementId);
            return Ok(movement);
        }

        [HttpPost("{stockItemId:int}/movements")]
        public async Task<ActionResult<StockMovementDto>> CreateMovement(
            int stockItemId,
            [FromBody] StockMovementCreateDto dto)
        {
            var created = await _movementService.CreateAsync(stockItemId, dto);
            return CreatedAtAction(
                nameof(GetMovementById),
                new { stockItemId, movementId = created.Id },
                created);
        }

        [HttpPatch("{stockItemId:int}/movements/{movementId:int}")]
        public async Task<ActionResult<StockMovementDto>> UpdateMovement(
            int stockItemId,
            int movementId,
            [FromBody] StockMovementUpdateDto dto)
        {
            var updated = await _movementService.UpdateAsync(stockItemId, movementId, dto);
            return Ok(updated);
        }

        [HttpDelete("{stockItemId:int}/movements/{movementId:int}")]
        public async Task<IActionResult> DeactivateMovement(int stockItemId, int movementId)
        {
            await _movementService.DeactivateAsync(stockItemId, movementId);
            return NoContent();
        }
    }
}
