using AutoMapper;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Interfaces;
using MuuBoi.DTOs;
using MuuBoi.Models;

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

        public async Task<IEnumerable<MedicationDto>> GetAllMedicationsAsync(string userId)
        {
            var medications = await _medicationRepository.GetAllMedicationsAsync(userId);
            return _mapper.Map<IEnumerable<MedicationDto>>(medications);
        }

        public async Task<MedicationDto?> GetMedicationByIdAsync(int id, string userId)
        {
            var medication = await _medicationRepository.GetMedicationByIdAsync(id);
            if (medication == null || medication.UserId != userId) return null;
            return _mapper.Map<MedicationDto>(medication);
        }

        public async Task<MedicationDto> CreateMedicationAsync(MedicationCreateDto dto, string userId)
        {
            var medication = _mapper.Map<Medication>(dto);
            medication.UserId = userId;
            var created = await _medicationRepository.CreateMedicationAsync(medication);
            return _mapper.Map<MedicationDto>(created);
        }

        public async Task<MedicationDto?> UpdateMedicationAsync(int id, MedicationUpdateDto dto, string userId)
        {
            var existing = await _medicationRepository.GetMedicationByIdAsync(id);
            if (existing == null || existing.UserId != userId) return null;

            _mapper.Map(dto, existing);
            existing.UpdatedAt = DateTime.UtcNow;
            var updated = await _medicationRepository.UpdateMedicationAsync(existing);
            return _mapper.Map<MedicationDto>(updated);
        }

        public async Task<MedicationDto?> DeleteMedicationAsync(int id, string userId)
        {
            var existing = await _medicationRepository.GetMedicationByIdAsync(id);
            if (existing == null || existing.UserId != userId) return null;

            var deleted = await _medicationRepository.DeleteMedicationAsync(id);
            return deleted == null ? null : _mapper.Map<MedicationDto>(deleted);
        }
    }
}
