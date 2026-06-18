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

        public async Task<DashboardDto> GetDashboardAsync(string userId)
        {
            return new DashboardDto
            {
                Cards = await _repo.GetCardsAsync(userId),
                GenderDistribution = await _repo.GetGenderDistributionAsync(userId),
                BreedDistribution = await _repo.GetBreedDistributionAsync(userId),
                VaccinesPerMonth = await _repo.GetVaccinesPerMonthAsync(userId),
                BirthForecast = await _repo.GetBirthForecastAsync(userId)
            };
        }
    }
}
