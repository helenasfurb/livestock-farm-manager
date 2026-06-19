using MuuBoi.DTOs;

namespace MuuBoi.Application.Interfaces
{
    public interface IDashboardRepository
    {
        Task<DashboardCardsDto> GetCardsAsync(string userId);
        Task<IEnumerable<GenderDistributionDto>> GetGenderDistributionAsync(string userId);
        Task<IEnumerable<BreedDistributionDto>> GetBreedDistributionAsync(string userId);
        Task<IEnumerable<VaccinePerMonthDto>> GetVaccinesPerMonthAsync(string userId, int months = 12);
        Task<IEnumerable<BirthForecastDto>> GetBirthForecastAsync(string userId);
    }
}
