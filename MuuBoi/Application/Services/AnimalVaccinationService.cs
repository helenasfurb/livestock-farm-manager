using System.ComponentModel.DataAnnotations;
using AutoMapper;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Interfaces;
using MuuBoi.Domain.Exceptions;
using MuuBoi.DTOs;
using MuuBoi.Interfaces;
using MuuBoi.Models;

namespace MuuBoi.Application.Services
{
    public class AnimalVaccinationService : IAnimalVaccinationService
    {
        private readonly IAnimalVaccinationRepository _vaccinationRepository;
        private readonly IAnimalRepository _animalRepository;
        private readonly IVaccineRepository _vaccineRepository;
        private readonly IMapper _mapper;

        public AnimalVaccinationService(
            IAnimalVaccinationRepository vaccinationRepository,
            IAnimalRepository animalRepository,
            IVaccineRepository vaccineRepository,
            IMapper mapper)
        {
            _vaccinationRepository = vaccinationRepository;
            _animalRepository = animalRepository;
            _vaccineRepository = vaccineRepository;
            _mapper = mapper;
        }

        public async Task<IEnumerable<AnimalVaccinationDto>> GetAllAnimalVaccinationsAsync(string animalId)
        {
            await FindAnimalAsync(animalId);
            var records = await _vaccinationRepository.GetAllAnimalVaccinationsAsync(int.Parse(animalId));
            return _mapper.Map<IEnumerable<AnimalVaccinationDto>>(records);
        }

        public async Task<AnimalVaccinationDto?> GetAnimalVaccinationByIdAsync(int id, string animalId)
        {
            await FindAnimalAsync(animalId);
            var record = await _vaccinationRepository.GetAnimalVaccinationByIdAsync(id, int.Parse(animalId));
            return record == null ? null : _mapper.Map<AnimalVaccinationDto>(record);
        }

        public async Task<AnimalVaccinationDto> CreateAnimalVaccinationAsync(AnimalVaccinationCreateDto dto, string animalId)
        {
            var animal = await FindAnimalAsync(animalId);
            var vaccine = await FindVaccineAsync(dto.VaccineId!.Value);

            var applicationDate = dto.ApplicationDate ?? DateTime.UtcNow;

            DateTime? nextApplicationDate = null;
            if (vaccine.RecommendedIntervalDays.HasValue)
            {
                var expectedNext = applicationDate.AddDays(vaccine.RecommendedIntervalDays.Value).Date;

                if (dto.NextApplicationDate.HasValue && dto.NextApplicationDate.Value.Date != expectedNext)
                    throw new ValidationException(
                        $"The next application date must be {expectedNext:yyyy-MM-dd} based on the vaccine's recommended interval of {vaccine.RecommendedIntervalDays} days.");

                nextApplicationDate = expectedNext;
            }
            else
            {
                nextApplicationDate = dto.NextApplicationDate;
            }

            var vaccination = new AnimalVaccination
            {
                AnimalId = animal.Id,
                VaccineId = dto.VaccineId.Value,
                ApplicationDate = applicationDate,
                NextApplicationDate = nextApplicationDate,
                BatchNumber = dto.BatchNumber,
                DosageMl = dto.DosageMl,
                Responsible = dto.Responsible,
                Observations = dto.Observations
            };

            var created = await _vaccinationRepository.CreateAnimalVaccinationAsync(vaccination);
            var withVaccine = await _vaccinationRepository.GetAnimalVaccinationByIdAsync(created.Id, animal.Id);
            return _mapper.Map<AnimalVaccinationDto>(withVaccine);
        }

        public async Task<AnimalVaccinationDto?> UpdateAnimalVaccinationAsync(int id, string animalId, AnimalVaccinationUpdateDto dto)
        {
            await FindAnimalAsync(animalId);

            if (dto.VaccineId.HasValue)
                await FindVaccineAsync(dto.VaccineId.Value);

            var existing = await _vaccinationRepository.GetAnimalVaccinationByIdAsync(id, int.Parse(animalId));
            if (existing == null) return null;

            _mapper.Map(dto, existing);
            var updated = await _vaccinationRepository.UpdateAnimalVaccinationAsync(existing);
            var withVaccine = await _vaccinationRepository.GetAnimalVaccinationByIdAsync(updated!.Id, int.Parse(animalId));
            return _mapper.Map<AnimalVaccinationDto>(withVaccine);
        }

        public async Task<AnimalVaccinationDto?> DeleteAnimalVaccinationAsync(int id, string animalId)
        {
            await FindAnimalAsync(animalId);
            var deleted = await _vaccinationRepository.DeleteAnimalVaccinationAsync(id, int.Parse(animalId));
            return deleted == null ? null : _mapper.Map<AnimalVaccinationDto>(deleted);
        }

        private async Task<Animal> FindAnimalAsync(string animalId)
        {
            var animal = await _animalRepository.GetAnimalByIdAsync(int.Parse(animalId));
            if (animal == null)
                throw new NotFoundException("Animal not found");
            return animal;
        }

        private async Task<Vaccine> FindVaccineAsync(int vaccineId)
        {
            var vaccine = await _vaccineRepository.GetVaccineByIdAsync(vaccineId);
            if (vaccine == null)
                throw new NotFoundException("Vaccine not found");
            return vaccine;
        }
    }
}
