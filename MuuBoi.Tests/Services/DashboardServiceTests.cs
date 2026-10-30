using Moq;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Interfaces;
using MuuBoi.Application.Services;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Tests.Services;

public class DashboardServiceTests
{
    private readonly Mock<IDashboardRepository> _repo = new();
    private readonly Mock<IAnimalService> _animalService = new();
    private readonly DashboardService _service;

    public DashboardServiceTests()
    {
        _service = new DashboardService(
            _repo.Object,
            new Mock<IMilkProductionRepository>().Object,
            new Mock<ILactationRepository>().Object,
            new Mock<IAnimalRepository>().Object,
            new Mock<IBreedingEventRepository>().Object,
            new Mock<IAnimalCalvingRepository>().Object,
            new Mock<IAnimalPregnancyRepository>().Object,
            _animalService.Object);
        _repo.Setup(r => r.GetVaccinationEventFactsAsync(It.IsAny<DateTime>())).ReturnsAsync(new List<VaccinationEventFact>());
        _animalService.Setup(s => s.GetAllAnimalsAsync(It.IsAny<AnimalFilterDto>())).ReturnsAsync(new List<AnimalListItemDto>());
    }

    private void SetupComposition(params AnimalBreed?[] breeds) =>
        _repo.Setup(r => r.GetActiveAnimalCompositionFactsAsync())
            .ReturnsAsync(breeds.Select(b => new AnimalCompositionFact { Breed = b }).ToList());

    [Fact]
    public async Task GetDashboardAsync_WithAnimalsWithoutBreed_AddsWithoutBreedEntryLast()
    {
        SetupComposition(AnimalBreed.Holstein, null, null, null);
        var result = await _service.GetDashboardAsync();
        var last = result.Herd.BreedDistribution.Last();
        Assert.Null(last.Breed);
        Assert.Equal("Sem raça registrada", last.BreedName);
        Assert.Equal(3, last.Count);
    }

    [Fact]
    public async Task GetDashboardAsync_WhenEveryAnimalHasBreed_DoesNotAddWithoutBreedEntry()
    {
        SetupComposition(AnimalBreed.Holstein, AnimalBreed.Jersey);
        var result = await _service.GetDashboardAsync();
        Assert.DoesNotContain(result.Herd.BreedDistribution, d => d.Breed == null);
    }

    [Fact]
    public async Task GetDashboardAsync_WithAnimalsWithoutBreed_DistributionSumsToTotalAnimals()
    {
        SetupComposition(AnimalBreed.Holstein, AnimalBreed.Jersey, AnimalBreed.Jersey, null);
        var result = await _service.GetDashboardAsync();
        Assert.Equal(result.Herd.TotalAnimals, result.Herd.BreedDistribution.Sum(d => d.Count));
    }
}
