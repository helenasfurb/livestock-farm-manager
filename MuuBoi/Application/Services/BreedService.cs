using AutoMapper;
using MuuBoi.DTOs;
using MuuBoi.Interfaces;
using MuuBoi.Models;

namespace MuuBoi.Services
{
    public class BreedService : IBreedService
    {
        private readonly IBreedRepository _breedRepository;
        private readonly IMapper _mapper;

        public BreedService(IBreedRepository breedRepository, IMapper mapper)
        {
            _breedRepository = breedRepository;
            _mapper = mapper;
        }

        public async Task<IEnumerable<BreedDto>> GetAllBreedsAsync(string userId)
        {
            var breeds = await _breedRepository.GetAllBreedsAsync(userId);
            return _mapper.Map<IEnumerable<BreedDto>>(breeds);
        }

        public async Task<BreedDto?> GetBreedByIdAsync(int id, string userId)
        {
            var breed = await _breedRepository.GetBreedByIdAsync(id);
            if (breed == null || breed.UserId != userId)
                return null;

            return _mapper.Map<BreedDto>(breed);
        }

        public async Task<BreedDto> CreateBreedAsync(BreedCreateDto breedCreateDto, string userId)
        {
            var breed = _mapper.Map<Breed>(breedCreateDto);
            breed.UserId = userId;

            var created = await _breedRepository.CreateBreedAsync(breed);
            return _mapper.Map<BreedDto>(created);
        }

        public async Task<BreedDto?> UpdateBreedAsync(int id, BreedUpdateDto breedUpdateDto, string userId)
        {
            var breed = await _breedRepository.GetBreedByIdAsync(id);
            if (breed == null || breed.UserId != userId)
                return null;

            _mapper.Map(breedUpdateDto, breed);
            breed.UpdatedAt = DateTime.UtcNow;

            var updated = await _breedRepository.UpdateBreedAsync(breed);
            return updated == null ? null : _mapper.Map<BreedDto>(updated);
        }

        public async Task<BreedDto?> DeleteBreedAsync(int id, string userId)
        {
            var breed = await _breedRepository.GetBreedByIdAsync(id);
            if (breed == null || breed.UserId != userId)
                return null;

            var deleted = await _breedRepository.DeleteBreedAsync(id);
            return deleted == null ? null : _mapper.Map<BreedDto>(deleted);
        }
    }
}
