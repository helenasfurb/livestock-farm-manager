using MuuBoi.Application.DTOs;

namespace MuuBoi.Application.Interfaces
{
    public interface ISemenSampleService
    {
        Task<IEnumerable<SemenSampleListItemDto>> GetAllAsync(SemenSampleFilterDto filter);
        Task<IEnumerable<SemenSampleAutocompleteItemDto>> GetAutocompleteAsync(string? name);
        Task<SemenSampleDto> GetByIdAsync(int id);
        Task<SemenSampleDto> CreateAsync(SemenSampleCreateDto dto);
        Task<SemenSampleDto> UpdateAsync(int id, SemenSampleUpdateDto dto);
        Task<bool> DeactivateAsync(int id);
        Task<bool> ReactivateAsync(int id);
    }
}
