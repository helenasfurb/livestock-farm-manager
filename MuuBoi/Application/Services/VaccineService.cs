using AutoMapper;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Interfaces;
using MuuBoi.Domain.Exceptions;
using MuuBoi.Domain.Models;

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

        public async Task<IEnumerable<VaccineDto>> GetAllVaccinesAsync(VaccineFilterDto filter)
        {
            var vaccines = await _vaccineRepository.GetAllVaccinesAsync(filter);
            return _mapper.Map<IEnumerable<VaccineDto>>(vaccines);
        }

        public async Task<VaccineDto> GetVaccineByIdAsync(int id)
        {
            var vaccine = await FindVaccineAsync(id);
            return _mapper.Map<VaccineDto>(vaccine);
        }

        public async Task<VaccineDto> CreateVaccineAsync(VaccineCreateDto dto)
        {
            var vaccine = _mapper.Map<Vaccine>(dto);
            var created = await _vaccineRepository.CreateVaccineAsync(vaccine);
            return _mapper.Map<VaccineDto>(created);
        }

        public async Task<VaccineDto> UpdateVaccineAsync(int id, VaccineUpdateDto dto)
        {
            var existing = await FindVaccineAsync(id);

            _mapper.Map(dto, existing);
            existing.UpdatedAt = DateTime.UtcNow;
            var updated = await _vaccineRepository.UpdateVaccineAsync(existing);
            return _mapper.Map<VaccineDto>(updated);
        }

        public async Task<bool> DeleteVaccineAsync(int id)
        {
            await FindVaccineAsync(id);

            await _vaccineRepository.DeleteVaccineAsync(id);
            return true;
        }

        private async Task<Vaccine> FindVaccineAsync(int id)
        {
            return await _vaccineRepository.GetVaccineByIdAsync(id)
                ?? throw new NotFoundException($"Vacina com id '{id}' não encontrada.");
        }
    }
}
