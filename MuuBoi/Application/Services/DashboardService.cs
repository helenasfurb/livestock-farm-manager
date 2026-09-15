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
        private readonly IBreedingEventRepository _breedingEventRepository;
        private readonly IAnimalCalvingRepository _animalCalvingRepository;
        private readonly IAnimalPregnancyRepository _animalPregnancyRepository;

        public DashboardService(
            IDashboardRepository repo,
            IMilkProductionRepository milkProductionRepository,
            ILactationRepository lactationRepository,
            IAnimalRepository animalRepository,
            IBreedingEventRepository breedingEventRepository,
            IAnimalCalvingRepository animalCalvingRepository,
            IAnimalPregnancyRepository animalPregnancyRepository)
        {
            _repo = repo;
            _milkProductionRepository = milkProductionRepository;
            _lactationRepository = lactationRepository;
            _animalRepository = animalRepository;
            _breedingEventRepository = breedingEventRepository;
            _animalCalvingRepository = animalCalvingRepository;
            _animalPregnancyRepository = animalPregnancyRepository;
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

        public async Task<ReproductiveDashboardDto> GetReproductiveDashboardAsync(ReproductiveDashboardFilterDto filter)
        {
            var today = DateTime.UtcNow.Date;
            var from = (filter.DateFrom ?? new DateTime(today.Year, today.Month, 1)).Date;
            var to = (filter.DateTo ?? today).Date;

            var (successful, unsuccessful, awaiting) = await _breedingEventRepository.GetStatusCountsAsync(from, to);
            var (rate, successfulCount, diagnosed) = ReproductiveDashboardResolver.ConceptionRate(successful, unsuccessful);

            var facts = await _animalRepository.GetReproductiveFactsAsync();
            var distribution = new ReproductiveStatusDistributionDto();
            var eligible = new List<AiEligibleAnimalDto>();

            foreach (var f in facts)
            {
                switch (ReproductiveStatusResolver.Resolve(
                    f.HasActiveConfirmedPregnancy, f.LastCalvingDate, f.LastAwaitingBreedingDate, today))
                {
                    case ReproductiveStatus.Open: distribution.Open++; break;
                    case ReproductiveStatus.AwaitingConfirmation: distribution.AwaitingConfirmation++; break;
                    case ReproductiveStatus.Pregnant: distribution.Pregnant++; break;
                    case ReproductiveStatus.Postpartum: distribution.Postpartum++; break;
                }

                if (ReproductiveDashboardResolver.IsEligibleForAI(
                    f.LastCalvingDate, f.HasActiveConfirmedPregnancy, f.LastAwaitingBreedingDate, today))
                {
                    eligible.Add(new AiEligibleAnimalDto
                    {
                        AnimalId = f.AnimalId,
                        Name = f.Name,
                        TagNumber = f.TagNumber,
                        LastCalvingDate = f.LastCalvingDate
                    });
                }
            }

            var totalServices = successful + unsuccessful + awaiting;

            var (fsSuccessful, fsUnsuccessful, _) = await _breedingEventRepository.GetFirstServiceStatusCountsAsync(from, to);
            var (fsRate, fsSuccessfulCount, fsDiagnosed) = ReproductiveDashboardResolver.ConceptionRate(fsSuccessful, fsUnsuccessful);

            var lostPregnancies = await _animalPregnancyRepository.GetLostCountAsync(from, to);

            var calvingRows = await _animalCalvingRepository.GetActiveCalvingDatesAsync();
            var calvingsByAnimal = calvingRows.ToLookup(c => c.AnimalId, c => c.CalvingDate);
            var calvingsPerAnimalDescending = calvingsByAnimal
                .Select(g => (IReadOnlyList<DateTime>)g.OrderByDescending(d => d).ToList());
            var averageCalvingInterval = ReproductiveDashboardResolver.AverageCalvingIntervalDays(
                calvingsPerAnimalDescending, from, to);

            var successfulBreedings = await _breedingEventRepository.GetSuccessfulBreedingsAsync(from, to);
            var averageDaysOpen = ReproductiveDashboardResolver.AverageDaysOpen(successfulBreedings, calvingsByAnimal);

            return new ReproductiveDashboardDto
            {
                DateFrom = from,
                DateTo = to,
                ConceptionRate = new ConceptionRateDto
                {
                    Rate = rate,
                    Successful = successfulCount,
                    Diagnosed = diagnosed,
                    AwaitingDiagnosis = awaiting
                },
                StatusDistribution = distribution,
                EligibleForAi = eligible
                    .OrderBy(a => a.LastCalvingDate)
                    .ToList(),
                FirstServiceConceptionRate = new ConceptionRateDto
                {
                    Rate = fsRate,
                    Successful = fsSuccessfulCount,
                    Diagnosed = fsDiagnosed
                },
                ServicesPerConception = ReproductiveDashboardResolver.ServicesPerConception(totalServices, successful),
                AverageDaysOpen = averageDaysOpen,
                AverageCalvingIntervalDays = averageCalvingInterval,
                LostPregnancies = lostPregnancies
            };
        }
    }
}
