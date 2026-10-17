using MuuBoi.Application.DTOs;

namespace MuuBoi.Application.Interfaces
{
    public interface IAnimalCalvingService
    {
        Task<AnimalCalvingCreatedDto> CreateAsync(int pregnancyId, AnimalCalvingCreateDto dto);
        Task<AnimalCalvingCalfDto> UpdateCalfAsync(int calvingId, int calfId, AnimalCalvingCalfUpdateDto dto);
        Task<bool> InactivateAsync(int id);
        Task<SyncPageDto<AnimalCalvingSyncDto>> GetChangesAsync(string? since, int? limit);
    }
}
