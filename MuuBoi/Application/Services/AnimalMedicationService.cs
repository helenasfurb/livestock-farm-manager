using AutoMapper;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Interfaces;
using MuuBoi.Domain.Exceptions;
using MuuBoi.Domain.Models;

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

        public async Task<IEnumerable<AnimalMedicationDto>> GetAllAnimalMedicationsAsync(int animalId)
        {
            await FindAnimalAsync(animalId);
            var records = await _animalMedicationRepository.GetAllAnimalMedicationsAsync(animalId);
            return _mapper.Map<IEnumerable<AnimalMedicationDto>>(records);
        }

        public async Task<AnimalMedicationDto> GetAnimalMedicationByIdAsync(int id, int animalId)
        {
            await FindAnimalAsync(animalId);
            var record = await FindAnimalMedicationAsync(id, animalId);
            return _mapper.Map<AnimalMedicationDto>(record);
        }

        public async Task<AnimalMedicationDto> CreateAnimalMedicationAsync(AnimalMedicationCreateDto dto, int animalId)
        {
            var animal = await FindAnimalAsync(animalId);
            await FindMedicationAsync(dto.MedicationId!.Value);

            var animalMedication = new AnimalMedication
            {
                AnimalId = animal.Id,
                MedicationId = dto.MedicationId.Value,
                Diagnosis = dto.Diagnosis,
                ApplicationDate = dto.ApplicationDate ?? DateTime.UtcNow,
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

        public async Task<AnimalMedicationDto> UpdateAnimalMedicationAsync(int id, int animalId, AnimalMedicationUpdateDto dto)
        {
            await FindAnimalAsync(animalId);

            if (dto.MedicationId.HasValue)
                await FindMedicationAsync(dto.MedicationId.Value);

            var existing = await FindAnimalMedicationAsync(id, animalId);

            _mapper.Map(dto, existing);
            var updated = await _animalMedicationRepository.UpdateAnimalMedicationAsync(existing);
            var withMedication = await _animalMedicationRepository.GetAnimalMedicationByIdAsync(updated!.Id, animalId);
            return _mapper.Map<AnimalMedicationDto>(withMedication);
        }

        public async Task<bool> DeleteAnimalMedicationAsync(int id, int animalId)
        {
            await FindAnimalAsync(animalId);
            await FindAnimalMedicationAsync(id, animalId);
            await _animalMedicationRepository.DeleteAnimalMedicationAsync(id, animalId);
            return true;
        }

        private async Task<Animal> FindAnimalAsync(int animalId)
        {
            return await _animalRepository.GetAnimalByIdAsync(animalId)
                ?? throw new NotFoundException($"Animal com id '{animalId}' não encontrado.");
        }

        private async Task<AnimalMedication> FindAnimalMedicationAsync(int id, int animalId)
        {
            return await _animalMedicationRepository.GetAnimalMedicationByIdAsync(id, animalId)
                ?? throw new NotFoundException($"Registro de medicação com id '{id}' não encontrado.");
        }

        private async Task FindMedicationAsync(int medicationId)
        {
            var medication = await _medicationRepository.GetMedicationByIdAsync(medicationId);
            if (medication == null)
                throw new NotFoundException($"Medicamento com id '{medicationId}' não encontrado.");
        }
    }
}
