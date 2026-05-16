using AutoMapper;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Interfaces;
using MuuBoi.Domain.Exceptions;
using MuuBoi.DTOs;
using MuuBoi.Interfaces;
using MuuBoi.Models;

namespace MuuBoi.Application.Services
{
    public class AnimalMedicationService : IAnimalMedicationService
    {
        private readonly IAnimalMedicationRepository _animalMedicationRepository;
        private readonly IAnimalRepository _animalRepository;
        private readonly IMedicationRepository _medicationRepository;
        private readonly IMapper _mapper;

        public AnimalMedicationService(
            IAnimalMedicationRepository animalMedicationRepository,
            IAnimalRepository animalRepository,
            IMedicationRepository medicationRepository,
            IMapper mapper)
        {
            _animalMedicationRepository = animalMedicationRepository;
            _animalRepository = animalRepository;
            _medicationRepository = medicationRepository;
            _mapper = mapper;
        }

        public async Task<IEnumerable<AnimalMedicationDto>> GetAllAnimalMedicationsAsync(string animalId)
        {
            await FindAnimalAsync(animalId);
            var records = await _animalMedicationRepository.GetAllAnimalMedicationsAsync(int.Parse(animalId));
            return _mapper.Map<IEnumerable<AnimalMedicationDto>>(records);
        }

        public async Task<AnimalMedicationDto?> GetAnimalMedicationByIdAsync(int id, string animalId)
        {
            await FindAnimalAsync(animalId);
            var record = await _animalMedicationRepository.GetAnimalMedicationByIdAsync(id, int.Parse(animalId));
            return record == null ? null : _mapper.Map<AnimalMedicationDto>(record);
        }

        public async Task<AnimalMedicationDto> CreateAnimalMedicationAsync(AnimalMedicationCreateDto dto, string animalId)
        {
            var animal = await FindAnimalAsync(animalId);
            await FindMedicationAsync(dto.MedicationId!.Value);

            var animalMedication = new AnimalMedication
            {
                AnimalId = animal.Id,
                MedicationId = dto.MedicationId.Value,
                Diagnosis = dto.Diagnosis,
                StartDate = dto.StartDate ?? DateTime.UtcNow,
                EndDate = dto.EndDate,
                DosageDescription = dto.DosageDescription,
                WithdrawalPeriodDays = dto.WithdrawalPeriodDays,
                Responsible = dto.Responsible,
                Observations = dto.Observations
            };

            var created = await _animalMedicationRepository.CreateAnimalMedicationAsync(animalMedication);
            var withMedication = await _animalMedicationRepository.GetAnimalMedicationByIdAsync(created.Id, animal.Id);
            return _mapper.Map<AnimalMedicationDto>(withMedication);
        }

        public async Task<AnimalMedicationDto?> UpdateAnimalMedicationAsync(int id, string animalId, AnimalMedicationUpdateDto dto)
        {
            await FindAnimalAsync(animalId);

            if (dto.MedicationId.HasValue)
                await FindMedicationAsync(dto.MedicationId.Value);

            var existing = await _animalMedicationRepository.GetAnimalMedicationByIdAsync(id, int.Parse(animalId));
            if (existing == null) return null;

            _mapper.Map(dto, existing);
            var updated = await _animalMedicationRepository.UpdateAnimalMedicationAsync(existing);
            var withMedication = await _animalMedicationRepository.GetAnimalMedicationByIdAsync(updated!.Id, int.Parse(animalId));
            return _mapper.Map<AnimalMedicationDto>(withMedication);
        }

        public async Task<AnimalMedicationDto?> DeleteAnimalMedicationAsync(int id, string animalId)
        {
            await FindAnimalAsync(animalId);
            var deleted = await _animalMedicationRepository.DeleteAnimalMedicationAsync(id, int.Parse(animalId));
            return deleted == null ? null : _mapper.Map<AnimalMedicationDto>(deleted);
        }

        private async Task<Animal> FindAnimalAsync(string animalId)
        {
            var animal = await _animalRepository.GetAnimalByIdAsync(int.Parse(animalId));
            if (animal == null)
                throw new NotFoundException("Animal not found");
            return animal;
        }

        private async Task FindMedicationAsync(int medicationId)
        {
            var medication = await _medicationRepository.GetMedicationByIdAsync(medicationId);
            if (medication == null)
                throw new NotFoundException("Medication not found");
        }
    }
}
