using AutoMapper;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Interfaces;
using MuuBoi.Domain.Exceptions;
using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Services
{
    public class MedicationService : IMedicationService
    {
        private readonly IMedicationRepository _medicationRepository;
        private readonly IMapper _mapper;

        public MedicationService(IMedicationRepository medicationRepository, IMapper mapper)
        {
            _medicationRepository = medicationRepository;
            _mapper = mapper;
        }

        public async Task<IEnumerable<MedicationDto>> GetAllMedicationsAsync()
        {
            var medications = await _medicationRepository.GetAllMedicationsAsync();
            return _mapper.Map<IEnumerable<MedicationDto>>(medications);
        }

        public async Task<MedicationDto> GetMedicationByIdAsync(int id)
        {
            var medication = await FindMedicationAsync(id);
            return _mapper.Map<MedicationDto>(medication);
        }

        public async Task<MedicationDto> CreateMedicationAsync(MedicationCreateDto dto)
        {
            var medication = _mapper.Map<Medication>(dto);
            var created = await _medicationRepository.CreateMedicationAsync(medication);
            return _mapper.Map<MedicationDto>(created);
        }

        public async Task<MedicationDto> UpdateMedicationAsync(int id, MedicationUpdateDto dto)
        {
            var existing = await FindMedicationAsync(id);

            _mapper.Map(dto, existing);
            existing.UpdatedAt = DateTime.UtcNow;
            var updated = await _medicationRepository.UpdateMedicationAsync(existing);
            return _mapper.Map<MedicationDto>(updated);
        }

        public async Task<bool> DeleteMedicationAsync(int id)
        {
            await FindMedicationAsync(id);

            await _medicationRepository.DeleteMedicationAsync(id);
            return true;
        }

        private async Task<Medication> FindMedicationAsync(int id)
        {
            return await _medicationRepository.GetMedicationByIdAsync(id)
                ?? throw new NotFoundException($"Medicamento com id '{id}' não encontrado.");
        }
    }
}
