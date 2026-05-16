using AutoMapper;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Interfaces;
using MuuBoi.DTOs;
using MuuBoi.Models;

namespace MuuBoi.Application.Services
{
    public class VaccineService : IVaccineService
    {
        private readonly IVaccineRepository _vaccineRepository;
        private readonly IMapper _mapper;

        public VaccineService(IVaccineRepository vaccineRepository, IMapper mapper)
        {
            _vaccineRepository = vaccineRepository;
            _mapper = mapper;
        }

        public async Task<IEnumerable<VaccineDto>> GetAllVaccinesAsync(string userId)
        {
            var vaccines = await _vaccineRepository.GetAllVaccinesAsync(userId);
            return _mapper.Map<IEnumerable<VaccineDto>>(vaccines);
        }

        public async Task<VaccineDto?> GetVaccineByIdAsync(int id, string userId)
        {
            var vaccine = await _vaccineRepository.GetVaccineByIdAsync(id);
            if (vaccine == null || vaccine.UserId != userId) return null;
            return _mapper.Map<VaccineDto>(vaccine);
        }

        public async Task<VaccineDto> CreateVaccineAsync(VaccineCreateDto dto, string userId)
        {
            var vaccine = _mapper.Map<Vaccine>(dto);
            vaccine.UserId = userId;
            var created = await _vaccineRepository.CreateVaccineAsync(vaccine);
            return _mapper.Map<VaccineDto>(created);
        }

        public async Task<VaccineDto?> UpdateVaccineAsync(int id, VaccineUpdateDto dto, string userId)
        {
            var existing = await _vaccineRepository.GetVaccineByIdAsync(id);
            if (existing == null || existing.UserId != userId) return null;

            _mapper.Map(dto, existing);
            existing.UpdatedAt = DateTime.UtcNow;
            var updated = await _vaccineRepository.UpdateVaccineAsync(existing);
            return _mapper.Map<VaccineDto>(updated);
        }

        public async Task<VaccineDto?> DeleteVaccineAsync(int id, string userId)
        {
            var existing = await _vaccineRepository.GetVaccineByIdAsync(id);
            if (existing == null || existing.UserId != userId) return null;

            var deleted = await _vaccineRepository.DeleteVaccineAsync(id);
            return deleted == null ? null : _mapper.Map<VaccineDto>(deleted);
        }
    }
}
