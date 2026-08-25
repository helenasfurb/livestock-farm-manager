using AutoMapper;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Interfaces;
using MuuBoi.Domain.Exceptions;
using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Services
{
    public class SemenSampleService : ISemenSampleService
    {
        private readonly ISemenSampleRepository _repository;
        private readonly ISemenSampleMovementService _movementService;
        private readonly IMapper _mapper;

        public SemenSampleService(
            ISemenSampleRepository repository,
            ISemenSampleMovementService movementService,
            IMapper mapper)
        {
            _repository = repository;
            _movementService = movementService;
            _mapper = mapper;
        }

        public async Task<IEnumerable<SemenSampleListItemDto>> GetAllAsync(SemenSampleFilterDto filter)
        {
            var samples = await _repository.GetAllAsync(filter);
            var sampleList = samples.ToList();

            var dtos = _mapper.Map<List<SemenSampleListItemDto>>(sampleList);

            var doses = await _repository.GetAvailableDosesBatchAsync(sampleList.Select(s => s.Id));
            foreach (var dto in dtos)
                dto.AvailableDoses = doses.GetValueOrDefault(dto.Id, 0);

            return dtos;
        }

        public async Task<IEnumerable<SemenSampleAutocompleteItemDto>> GetAutocompleteAsync(string? name)
        {
            var samples = await _repository.GetAutocompleteAsync(name);
            return _mapper.Map<IEnumerable<SemenSampleAutocompleteItemDto>>(samples);
        }

        public async Task<SemenSampleDto> GetByIdAsync(int id)
        {
            var sample = await _repository.GetByIdAsync(id)
                ?? throw new NotFoundException($"Amostra de sêmen com id '{id}' não encontrada.");

            var dto = _mapper.Map<SemenSampleDto>(sample);
            dto.AvailableDoses = await _repository.GetAvailableDosesAsync(id);
            return dto;
        }

        public async Task<SemenSampleDto> CreateAsync(SemenSampleCreateDto dto)
        {
            var sample = _mapper.Map<SemenSample>(dto);
            var created = await _repository.CreateAsync(sample);

            if (dto.InitialQuantity.HasValue)
                await _movementService.CreateForSemenSampleAsync(created.Id, dto.InitialQuantity.Value, dto.InitialNotes);

            var result = _mapper.Map<SemenSampleDto>(created);
            result.AvailableDoses = await _repository.GetAvailableDosesAsync(created.Id);
            return result;
        }

        public async Task<SemenSampleDto> UpdateAsync(int id, SemenSampleUpdateDto dto)
        {
            var sample = await _repository.GetByIdAsync(id)
                ?? throw new NotFoundException($"Amostra de sêmen com id '{id}' não encontrada.");

            _mapper.Map(dto, sample);
            var updated = await _repository.UpdateAsync(sample);

            var result = _mapper.Map<SemenSampleDto>(updated);
            result.AvailableDoses = await _repository.GetAvailableDosesAsync(id);
            return result;
        }

        public async Task<bool> DeactivateAsync(int id)
        {
            var sample = await _repository.GetByIdAsync(id)
                ?? throw new NotFoundException($"Amostra de sêmen com id '{id}' não encontrada.");

            if (!sample.IsActive)
                throw new ConflictException("A amostra de sêmen já está inativa.");

            sample.IsActive = false;
            sample.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(sample);
            return false;
        }

        public async Task<bool> ReactivateAsync(int id)
        {
            var sample = await _repository.GetByIdAsync(id)
                ?? throw new NotFoundException($"Amostra de sêmen com id '{id}' não encontrada.");

            if (sample.IsActive)
                throw new ConflictException("A amostra de sêmen já está ativa.");

            sample.IsActive = true;
            sample.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(sample);
            return true;
        }
    }
}
