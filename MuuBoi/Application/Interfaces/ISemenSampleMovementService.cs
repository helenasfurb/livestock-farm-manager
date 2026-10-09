using MuuBoi.Application.DTOs;
using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Interfaces
{
    public interface ISemenSampleMovementService
    {
        Task<IEnumerable<SemenSampleMovementListItemDto>> GetBySemenSampleIdAsync(int semenSampleId, SemenSampleMovementFilterDto filter);
        Task<SemenSampleMovementDto> GetByIdAsync(int semenSampleId, int movementId);
        Task<SemenSampleMovementDto> CreateAsync(int semenSampleId, SemenSampleMovementCreateDto dto);
        Task<SemenSampleMovementDto> UpdateAsync(int semenSampleId, int movementId, SemenSampleMovementUpdateDto dto);
        Task DeactivateAsync(int semenSampleId, int movementId);
        Task CreateForBreedingEventAsync(BreedingEvent breedingEvent);
        Task InactivateForBreedingEventAsync(int breedingEventId);
        Task<SyncPageDto<SemenSampleMovementDto>> GetChangesAsync(string? since, int? limit);
    }
}
