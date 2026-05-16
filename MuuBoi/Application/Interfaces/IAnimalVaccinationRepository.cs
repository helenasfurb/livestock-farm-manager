using MuuBoi.Models;

namespace MuuBoi.Application.Interfaces
{
    public interface IAnimalVaccinationRepository
    {
        Task<IEnumerable<AnimalVaccination>> GetAllAnimalVaccinationsAsync(int animalId);
        Task<AnimalVaccination?> GetAnimalVaccinationByIdAsync(int id, int animalId);
        Task<AnimalVaccination> CreateAnimalVaccinationAsync(AnimalVaccination vaccination);
        Task<AnimalVaccination?> UpdateAnimalVaccinationAsync(AnimalVaccination vaccination);
        Task<AnimalVaccination?> DeleteAnimalVaccinationAsync(int id, int animalId);
    }
}
