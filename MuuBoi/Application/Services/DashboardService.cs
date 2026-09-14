using MuuBoi.Application.Interfaces;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Helpers;
using MuuBoi.Domain.Enums;
using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly IDashboardRepository _repo;
        private readonly IMilkProductionRepository _milkProductionRepository;
        private readonly ILactationRepository _lactationRepository;
        private readonly IAnimalRepository _animalRepository;

        public DashboardService(
            IDashboardRepository repo,
            IMilkProductionRepository milkProductionRepository,
            ILactationRepository lactationRepository,
            IAnimalRepository animalRepository)
        {
            _repo = repo;
            _milkProductionRepository = milkProductionRepository;
            _lactationRepository = lactationRepository;
            _animalRepository = animalRepository;
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

        public async Task<ProductiveDashboardDto> GetProductiveDashboardAsync(ProductiveDashboardFilterDto filter)
        {
            var today = DateTime.UtcNow.Date;
            var from = (filter.DateFrom ?? new DateTime(today.Year, today.Month, 1)).Date;
            var to = (filter.DateTo ?? today).Date;

            var totalVolume = await _milkProductionRepository.GetTotalVolumeAsync(from, to);

            var femaleIds = await _animalRepository.GetAdultFemaleIdsAsync();
            var activeLactations = femaleIds.Count > 0
                ? await _lactationRepository.GetActiveByAnimalIdsAsync(femaleIds)
                : Enumerable.Empty<Lactation>();
            var lactationsByAnimal = activeLactations
                .GroupBy(l => l.AnimalId)
                .ToDictionary(g => g.Key, g => (IEnumerable<Lactation>)g.ToList());

            var distribution = new ProductivePhaseDistributionDto();
            foreach (var id in femaleIds)
            {
                var animalLactations = lactationsByAnimal.TryGetValue(id, out var ls)
                    ? ls : Enumerable.Empty<Lactation>();
                switch (ProductiveStatusResolver.Resolve(animalLactations, today))
                {
                    case ProductiveStatus.Lactating: distribution.Lactating++; break;
                    case ProductiveStatus.Dry: distribution.Dry++; break;
                    default: distribution.NeverLactated++; break;
                }
            }

            return new ProductiveDashboardDto
            {
                DateFrom = from,
                DateTo = to,
                TotalVolume = totalVolume,
                AveragePerLactatingCow = ProductiveDashboardResolver.AveragePerLactatingCow(totalVolume, distribution.Lactating),
                PhaseDistribution = distribution
            };
        }
    }
}
