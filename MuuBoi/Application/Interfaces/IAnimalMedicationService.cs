using MuuBoi.Application.DTOs;
using MuuBoi.DTOs;

namespace MuuBoi.Application.Interfaces
{
    public interface IAnimalMedicationService
    {
        Task<IEnumerable<AnimalMedicationDto>> GetAllAnimalMedicationsAsync(string animalId);
        Task<AnimalMedicationDto?> GetAnimalMedicationByIdAsync(int id, string animalId);
        Task<AnimalMedicationDto> CreateAnimalMedicationAsync(AnimalMedicationCreateDto dto, string animalId);
        Task<AnimalMedicationDto?> UpdateAnimalMedicationAsync(int id, string animalId, AnimalMedicationUpdateDto dto);
        Task<AnimalMedicationDto?> DeleteAnimalMedicationAsync(int id, string animalId);
    }
}
