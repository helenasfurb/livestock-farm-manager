using AutoMapper;
using Moq;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Interfaces;
using MuuBoi.Application.Mappings;
using MuuBoi.Application.Services;
using MuuBoi.Domain.Enums;
using MuuBoi.Domain.Exceptions;
using MuuBoi.Domain.Models;

namespace MuuBoi.Tests.Services;

public class HealthCaseServiceTests
{
    private readonly Mock<IHealthCaseRepository> _repository = new();
    private readonly Mock<IAnimalRepository> _animalRepository = new();
    private readonly HealthCaseService _service;

    public HealthCaseServiceTests()
    {
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<HealthCaseProfile>()).CreateMapper();
        _service = new HealthCaseService(_repository.Object, _animalRepository.Object, mapper);
        _repository.Setup(r => r.CreateAsync(It.IsAny<HealthCase>())).ReturnsAsync((HealthCase c) =>
        {
            c.Id = 10;
            return c;
        });
        _repository.Setup(r => r.GetByIdAsync(10)).ReturnsAsync(() => new HealthCase { Id = 10, AnimalId = 1 });
    }

    private void SetupAnimal(AnimalGender? gender, AnimalClassification? classification)
    {
        _animalRepository.Setup(r => r.GetAnimalByIdAsync(1))
            .ReturnsAsync(new Animal { Id = 1, Gender = gender, Classification = classification });
    }

    private static HealthCaseCreateDto BuildDto(DiseaseType diseaseType)
    {
        return new HealthCaseCreateDto
        {
            AnimalId = 1,
            DiseaseType = diseaseType,
            DiseaseName = diseaseType == DiseaseType.Other ? "Pneumonia" : null,
            DiagnosisDate = DateTime.UtcNow.Date
        };
    }

    [Fact]
    public async Task CreateAsync_MastitisForMaleAnimal_ThrowsBusinessRuleException()
    {
        SetupAnimal(AnimalGender.M, AnimalClassification.Calf);
        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.CreateAsync(BuildDto(DiseaseType.Mastitis)));
        _repository.Verify(r => r.CreateAsync(It.IsAny<HealthCase>()), Times.Never);
    }

    [Theory]
    [InlineData(AnimalClassification.Bull)]
    [InlineData(AnimalClassification.Steer)]
    public async Task CreateAsync_MastitisForBullOrSteer_ThrowsBusinessRuleException(AnimalClassification classification)
    {
        SetupAnimal(AnimalGender.F, classification);
        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.CreateAsync(BuildDto(DiseaseType.Mastitis)));
        _repository.Verify(r => r.CreateAsync(It.IsAny<HealthCase>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_MastitisForCow_CreatesHealthCase()
    {
        SetupAnimal(AnimalGender.F, AnimalClassification.Cow);
        var result = await _service.CreateAsync(BuildDto(DiseaseType.Mastitis));
        Assert.Equal(10, result.Id);
        _repository.Verify(r => r.CreateAsync(It.Is<HealthCase>(c => c.DiseaseType == DiseaseType.Mastitis)), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_NonMastitisForMaleAnimal_CreatesHealthCase()
    {
        SetupAnimal(AnimalGender.M, AnimalClassification.Bull);
        var result = await _service.CreateAsync(BuildDto(DiseaseType.Other));
        Assert.Equal(10, result.Id);
        _repository.Verify(r => r.CreateAsync(It.Is<HealthCase>(c => c.DiseaseType == DiseaseType.Other)), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithNonexistentAnimal_ThrowsNotFoundException()
    {
        _animalRepository.Setup(r => r.GetAnimalByIdAsync(1)).ReturnsAsync((Animal?)null);
        await Assert.ThrowsAsync<NotFoundException>(() => _service.CreateAsync(BuildDto(DiseaseType.Mastitis)));
    }
}
