using MuuBoi.Application.DTOs;

namespace MuuBoi.Application.Interfaces
{
    public interface IStockReferenceService
    {
        Task<IEnumerable<StockCategoryDto>> GetCategoriesAsync();
        Task<IEnumerable<UnitOfMeasureDto>> GetUnitsAsync();
    }
}
