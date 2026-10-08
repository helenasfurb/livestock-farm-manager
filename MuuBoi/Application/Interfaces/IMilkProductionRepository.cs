using MuuBoi.Application.DTOs;
using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Interfaces
{
    public interface IMilkProductionRepository
    {
        Task<IEnumerable<MilkProduction>> GetAllAsync(MilkProductionFilterDto filter);
        Task<decimal> GetTotalVolumeAsync(DateTime from, DateTime to);
        Task<MilkProduction?> GetByIdAsync(int id);
        Task<MilkProduction?> GetBySyncIdAsync(Guid syncId);
        Task<MilkProduction> CreateAsync(MilkProduction milkProduction);
        Task<MilkProduction> UpdateAsync(MilkProduction milkProduction);
        Task<IReadOnlyList<MilkProduction>> GetChangesAsync(ulong since, int take);
    }
}
