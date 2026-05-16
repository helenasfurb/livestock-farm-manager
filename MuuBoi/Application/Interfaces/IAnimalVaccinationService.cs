using MuuBoi.Application.DTOs;
using MuuBoi.DTOs;

namespace MuuBoi.Application.Interfaces
{
    public interface IAnimalVaccinationService
    {
        Task<IEnumerable<AnimalVaccinationDto>> GetAllAnimalVaccinationsAsync(string animalId);
        Task<AnimalVaccinationDto?> GetAnimalVaccinationByIdAsync(int id, string animalId);
        Task<AnimalVaccinationDto> CreateAnimalVaccinationAsync(AnimalVaccinationCreateDto dto, string animalId);
        Task<AnimalVaccinationDto?> UpdateAnimalVaccinationAsync(int id, string animalId, AnimalVaccinationUpdateDto dto);
        Task<AnimalVaccinationDto?> DeleteAnimalVaccinationAsync(int id, string animalId);
    }
}
