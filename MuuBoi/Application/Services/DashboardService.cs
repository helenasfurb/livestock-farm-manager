using MuuBoi.Application.Interfaces;
using MuuBoi.DTOs;

namespace MuuBoi.Application.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly IDashboardRepository _repo;

        public DashboardService(IDashboardRepository repo)
        {
            _repo = repo;
        }

        public async Task<DashboardDto> GetDashboardAsync()
        {
            return new DashboardDto
            {
                Cards = await _repo.GetCardsAsync(),
                GenderDistribution = await _repo.GetGenderDistributionAsync(),
                BreedDistribution = await _repo.GetBreedDistributionAsync(),
                VaccinesPerMonth = await _repo.GetVaccinesPerMonthAsync(),
                BirthForecast = await _repo.GetBirthForecastAsync()
            };
        }
    }
}
