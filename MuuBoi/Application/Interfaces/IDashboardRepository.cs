using MuuBoi.DTOs;

namespace MuuBoi.Application.Interfaces
{
    public interface IDashboardRepository
    {
        Task<DashboardCardsDto> GetCardsAsync();
        Task<IEnumerable<GenderDistributionDto>> GetGenderDistributionAsync();
        Task<IEnumerable<BreedDistributionDto>> GetBreedDistributionAsync();
        Task<IEnumerable<VaccinePerMonthDto>> GetVaccinesPerMonthAsync(int months = 12);
        Task<IEnumerable<BirthForecastDto>> GetBirthForecastAsync();
    }
}
