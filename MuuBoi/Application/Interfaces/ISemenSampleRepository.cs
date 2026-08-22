using MuuBoi.Application.DTOs;
using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Interfaces
{
    public interface ISemenSampleRepository
    {
        Task<IEnumerable<SemenSample>> GetAllAsync(SemenSampleFilterDto filter);
        Task<IEnumerable<SemenSample>> GetAutocompleteAsync(string? name);
        Task<SemenSample?> GetByIdAsync(int id);
        Task<SemenSample> CreateAsync(SemenSample semenSample);
        Task<SemenSample> UpdateAsync(SemenSample semenSample);
    }
}
