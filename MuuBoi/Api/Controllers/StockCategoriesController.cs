using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Interfaces;

namespace MuuBoi.Api.Controllers
{
    [ApiController]
    [Route("api/stock-categories")]
    [Authorize]
    public class StockCategoriesController : ControllerBase
    {
        private readonly IStockReferenceService _service;

        public StockCategoriesController(IStockReferenceService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<StockCategoryDto>>> GetAll()
        {
            var categories = await _service.GetCategoriesAsync();
            return Ok(categories);
        }
    }
}
