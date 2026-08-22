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
        private readonly IMapper _mapper;

        public SemenSampleService(ISemenSampleRepository repository, IMapper mapper)
        {
            _repository = repository;
            _mapper = mapper;
        }

        public async Task<IEnumerable<SemenSampleListItemDto>> GetAllAsync(SemenSampleFilterDto filter)
        {
            var samples = await _repository.GetAllAsync(filter);
            return _mapper.Map<IEnumerable<SemenSampleListItemDto>>(samples);
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
            return _mapper.Map<SemenSampleDto>(sample);
        }

        public async Task<SemenSampleDto> CreateAsync(SemenSampleCreateDto dto)
        {
            var sample = _mapper.Map<SemenSample>(dto);
            var created = await _repository.CreateAsync(sample);
            return _mapper.Map<SemenSampleDto>(created);
        }

        public async Task<SemenSampleDto> UpdateAsync(int id, SemenSampleUpdateDto dto)
        {
            var sample = await _repository.GetByIdAsync(id)
                ?? throw new NotFoundException($"Amostra de sêmen com id '{id}' não encontrada.");

            _mapper.Map(dto, sample);
            var updated = await _repository.UpdateAsync(sample);
            return _mapper.Map<SemenSampleDto>(updated);
        }

        public async Task DeactivateAsync(int id)
        {
            var sample = await _repository.GetByIdAsync(id)
                ?? throw new NotFoundException($"Amostra de sêmen com id '{id}' não encontrada.");

            if (!sample.IsActive)
                throw new ConflictException("A amostra de sêmen já está inativa.");

            sample.IsActive = false;
            sample.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(sample);
        }
    }
}
