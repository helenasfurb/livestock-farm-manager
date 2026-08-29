using MuuBoi.Application.DTOs;

namespace MuuBoi.Application.Interfaces
{
    public interface IAnimalCalvingService
    {
        Task<AnimalCalvingDto> CreateAsync(int pregnancyId, AnimalCalvingCreateDto dto);
        Task<AnimalCalvingCalfDto> UpdateCalfAsync(int calvingId, int calfId, AnimalCalvingCalfUpdateDto dto);
        Task<bool> InactivateAsync(int id);
    }
}
