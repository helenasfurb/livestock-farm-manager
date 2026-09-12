using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Interfaces
{
    public interface IStockReferenceRepository
    {
        Task<IEnumerable<StockCategory>> GetCategoriesAsync();
        Task<IEnumerable<UnitOfMeasure>> GetUnitsAsync();
        Task<bool> CategoryExistsAsync(int id);
        Task<bool> UnitExistsAsync(int id);
    }
}
