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
        private readonly IAnimalService _animalService;

        public DashboardService(
            IDashboardRepository repo,
            IMilkProductionRepository milkProductionRepository,
            ILactationRepository lactationRepository,
            IAnimalRepository animalRepository,
            IBreedingEventRepository breedingEventRepository,
            IAnimalCalvingRepository animalCalvingRepository,
            IAnimalPregnancyRepository animalPregnancyRepository,
            IAnimalService animalService)
        {
            _repo = repo;
            _milkProductionRepository = milkProductionRepository;
            _lactationRepository = lactationRepository;
            _animalRepository = animalRepository;
            _breedingEventRepository = breedingEventRepository;
            _animalCalvingRepository = animalCalvingRepository;
            _animalPregnancyRepository = animalPregnancyRepository;
            _animalService = animalService;
        }

        public async Task<DashboardDto> GetDashboardAsync()
        {
            var today = DateTime.UtcNow.Date;
            var vaccineCutoff = today.AddMonths(-12);

            var composition = await _repo.GetActiveAnimalCompositionFactsAsync();
            var vaccinationFacts = await _repo.GetVaccinationEventFactsAsync(vaccineCutoff);

            var underTreatment = (await _animalService.GetAllAnimalsAsync(
                new AnimalFilterDto { IsActive = true, SanitaryStatus = SanitaryStatus.UnderTreatment }))
                .ToList();

            return new DashboardDto
            {
                Herd = BuildHerdComposition(composition),
                Sanitary = new SanitaryPulseDto
                {
                    UnderTreatment = new AnimalsUnderTreatmentDto
                    {
                        Count = underTreatment.Count,
                        Animals = underTreatment
                    },
                    OverdueVaccinations = BuildOverdueVaccinations(vaccinationFacts, today)
                },
                VaccinesPerMonth = BuildVaccinesPerMonth(vaccinationFacts, today)
            };
        }

        private static HerdCompositionDto BuildHerdComposition(IEnumerable<AnimalCompositionFact> facts)
        {
            var list = facts as IList<AnimalCompositionFact> ?? facts.ToList();
            return new HerdCompositionDto
            {
                TotalAnimals = list.Count,
                ClassificationDistribution = list
                    .Where(f => f.Classification.HasValue)
                    .GroupBy(f => f.Classification!.Value)
                    .Select(g => new ClassificationDistributionDto
                    {
                        Classification = g.Key,
                        Label = g.Key.GetDescription(),
                        Count = g.Count()
                    })
                    .OrderByDescending(d => d.Count)
                    .ToList(),
                GenderDistribution = list
                    .Where(f => f.Gender.HasValue)
                    .GroupBy(f => f.Gender!.Value)
                    .Select(g => new GenderDistributionDto
                    {
                        Gender = g.Key.ToString(),
                        Label = g.Key.GetDescription(),
                        Count = g.Count()
                    })
                    .OrderBy(d => d.Gender)
                    .ToList(),
                BreedDistribution = list
                    .Where(f => f.Breed.HasValue)
                    .GroupBy(f => f.Breed!.Value)
                    .Select(g => new BreedDistributionDto
                    {
                        Breed = g.Key,
                        BreedName = g.Key.GetDescription(),
                        Count = g.Count()
                    })
                    .OrderByDescending(d => d.Count)
                    .ToList()
            };
        }

        private static IEnumerable<VaccinePerMonthDto> BuildVaccinesPerMonth(
            IEnumerable<VaccinationEventFact> facts, DateTime today)
        {
            return facts
                .Where(f => f.ApplicationDate.HasValue
                    && VaccinationEventStatusResolver.Resolve(f.ApplicationDate, f.PredictedDate, today)
                        == VaccinationEventStatus.Applied)
                .GroupBy(f => new { f.ApplicationDate!.Value.Year, f.ApplicationDate!.Value.Month })
                .Select(g => new VaccinePerMonthDto
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    MonthLabel = $"{new DateTime(g.Key.Year, g.Key.Month, 1):MMM/yyyy}",
                    Count = g.Sum(f => f.AnimalCount)
                })
                .OrderBy(x => x.Year).ThenBy(x => x.Month)
                .ToList();
        }

        private static OverdueVaccinationsDto BuildOverdueVaccinations(
            IEnumerable<VaccinationEventFact> facts, DateTime today)
        {
            var overdue = facts
                .Where(f => VaccinationEventStatusResolver.Resolve(f.ApplicationDate, f.PredictedDate, today)
                    == VaccinationEventStatus.Overdue)
                .Select(f => new OverdueVaccinationItemDto
                {
                    VaccinationEventId = f.VaccinationEventId,
                    VaccineName = f.VaccineName,
                    PredictedDate = f.PredictedDate ?? default,
                    AnimalCount = f.AnimalCount
                })
                .OrderBy(e => e.PredictedDate)
                .ToList();

            return new OverdueVaccinationsDto
            {
                Count = overdue.Count,
                Events = overdue
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

            var forecastRows = await _animalPregnancyRepository.GetActiveConfirmedForForecastAsync();
            var calvingForecast = forecastRows
                .Select(p => new CalvingForecastItemDto
                {
                    AnimalId = p.AnimalId,
                    Name = p.Animal?.Name,
                    TagNumber = p.Animal?.TagNumber,
                    PregnancyId = p.Id,
                    ExpectedCalvingDate = p.ExpectedCalvingDate
                })
                .ToList();

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
                LostPregnancies = lostPregnancies,
                CalvingForecast = calvingForecast
            };
        }
    }
}
