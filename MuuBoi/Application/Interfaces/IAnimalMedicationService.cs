using MuuBoi.Application.DTOs;

namespace MuuBoi.Application.Interfaces
{
    public interface IAnimalMedicationService
    {
        Task<IEnumerable<AnimalMedicationDto>> GetAllAnimalMedicationsAsync(int animalId);
        Task<AnimalMedicationDto> GetAnimalMedicationByIdAsync(int id, int animalId);
        Task<AnimalMedicationDto> CreateAnimalMedicationAsync(AnimalMedicationCreateDto dto, int animalId);
        Task<AnimalMedicationDto> UpdateAnimalMedicationAsync(int id, int animalId, AnimalMedicationUpdateDto dto);
        Task<bool> DeleteAnimalMedicationAsync(int id, int animalId);
    }
}
