using System.ComponentModel.DataAnnotations;
using AutoMapper;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Helpers;
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
            if (dto.SyncId.HasValue)
            {
                var existing = await _vaccineRepository.GetVaccineBySyncIdAsync(dto.SyncId.Value);
                if (existing != null)
                    return _mapper.Map<VaccineDto>(existing);
            }

            var vaccine = _mapper.Map<Vaccine>(dto);
            vaccine.SyncId = dto.SyncId ?? Guid.NewGuid();
            var created = await _vaccineRepository.CreateVaccineAsync(vaccine);
            return _mapper.Map<VaccineDto>(created);
        }

        public async Task<VaccineDto> UpdateVaccineAsync(int id, VaccineUpdateDto dto)
        {
            var existing = await FindVaccineAsync(id);

            var editedAt = SyncTimestampResolver.ResolveEditedAt(dto.UpdatedAt, DateTime.UtcNow);
            if (SyncTimestampResolver.IsOutdated(editedAt, existing))
                return _mapper.Map<VaccineDto>(existing);

            _mapper.Map(dto, existing);
            existing.UpdatedAt = editedAt;
            var updated = await _vaccineRepository.UpdateVaccineAsync(existing);
            return _mapper.Map<VaccineDto>(updated);
        }

        public async Task<bool> DeleteVaccineAsync(int id)
        {
            var vaccine = await FindVaccineAsync(id);
            if (!vaccine.IsActive)
                return true;

            await _vaccineRepository.DeleteVaccineAsync(id);
            return true;
        }

        public async Task<SyncPageDto<VaccineDto>> GetChangesAsync(string? since, int? limit)
        {
            if (!SyncPaging.TryDecodeCursor(since, out var cursor))
                throw new ValidationException("Cursor de sincronização inválido.");

            var take = SyncPaging.ResolveLimit(limit);
            var fetched = await _vaccineRepository.GetChangesAsync(cursor, take + 1);

            return SyncPaging.BuildPage(fetched, take, cursor, v => _mapper.Map<VaccineDto>(v));
        }

        private async Task<Vaccine> FindVaccineAsync(int id)
        {
            return await _vaccineRepository.GetVaccineByIdAsync(id)
                ?? throw new NotFoundException($"Vacina com id '{id}' não encontrada.");
        }
    }
}
