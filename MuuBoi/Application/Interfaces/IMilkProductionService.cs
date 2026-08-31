using MuuBoi.Application.DTOs;

namespace MuuBoi.Application.Interfaces
{
    public interface IMilkProductionService
    {
        Task<IEnumerable<MilkProductionDayDto>> GetAllAsync(MilkProductionFilterDto filter);
        Task<MilkProductionDto> GetByIdAsync(int id);
        Task<MilkProductionDto> CreateAsync(MilkProductionCreateDto dto);
        Task<MilkProductionDto> UpdateAsync(int id, MilkProductionUpdateDto dto);
        Task<bool> DeactivateAsync(int id);
    }
}
