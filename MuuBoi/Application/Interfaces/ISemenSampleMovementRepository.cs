using MuuBoi.Application.DTOs;
using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Interfaces
{
    public interface ISemenSampleMovementRepository
    {
        Task<IEnumerable<SemenSampleMovement>> GetBySemenSampleIdAsync(int semenSampleId, SemenSampleMovementFilterDto filter);
        Task<SemenSampleMovement?> GetByIdAsync(int id);
        Task<SemenSampleMovement?> GetByBreedingEventIdAsync(int breedingEventId);
        Task<SemenSampleMovement> CreateAsync(SemenSampleMovement movement);
        Task<SemenSampleMovement> UpdateAsync(SemenSampleMovement movement);
    }
}
