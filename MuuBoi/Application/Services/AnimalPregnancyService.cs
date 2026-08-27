using AutoMapper;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Interfaces;
using MuuBoi.Domain.Enums;
using MuuBoi.Domain.Exceptions;
using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Services
{
    public class AnimalPregnancyService : IAnimalPregnancyService
    {
        private const int GestationDays = 280;

        private readonly IAnimalPregnancyRepository _repository;
        private readonly IAnimalCalvingRepository _calvingRepository;
        private readonly IAnimalRepository _animalRepository;
        private readonly IMapper _mapper;

        public AnimalPregnancyService(
            IAnimalPregnancyRepository repository,
            IAnimalCalvingRepository calvingRepository,
            IAnimalRepository animalRepository,
            IMapper mapper)
        {
            _repository = repository;
            _calvingRepository = calvingRepository;
            _animalRepository = animalRepository;
            _mapper = mapper;
        }

        public async Task<IEnumerable<AnimalPregnancyListItemDto>> GetAllAsync(AnimalPregnancyFilterDto filter)
        {
            var pregnancies = await _repository.GetAllAsync(filter);
            return _mapper.Map<IEnumerable<AnimalPregnancyListItemDto>>(pregnancies);
        }

        public async Task<IEnumerable<AnimalPregnancyListItemDto>> GetByAnimalIdAsync(int animalId, bool? isActive)
        {
            _ = await _animalRepository.GetAnimalByIdAsync(animalId)
                ?? throw new NotFoundException($"Animal com id '{animalId}' não encontrado.");

            var pregnancies = await _repository.GetByAnimalIdAsync(animalId, isActive);
            return _mapper.Map<IEnumerable<AnimalPregnancyListItemDto>>(pregnancies);
        }

        public async Task<AnimalPregnancyDto> GetByIdAsync(int id)
        {
            var pregnancy = await _repository.GetByIdAsync(id)
                ?? throw new NotFoundException($"Gestação com id '{id}' não encontrada.");
            return _mapper.Map<AnimalPregnancyDto>(pregnancy);
        }

        public async Task<AnimalPregnancyDto> RegisterLossAsync(int id, AnimalPregnancyStatusUpdateDto dto)
        {
            var pregnancy = await _repository.GetByIdAsync(id)
                ?? throw new NotFoundException($"Gestação com id '{id}' não encontrada.");

            if (pregnancy.Status != AnimalPregnancyStatus.Confirmed)
                throw new ConflictException("Apenas gestações confirmadas podem ser marcadas como interrompidas.");

            pregnancy.Status = AnimalPregnancyStatus.LostPregnancy;
            pregnancy.LossDate = dto.LossDate;
            if (dto.Notes != null)
                pregnancy.Notes = dto.Notes;
            pregnancy.UpdatedAt = DateTime.UtcNow;

            var updated = await _repository.UpdateAsync(pregnancy);
            return _mapper.Map<AnimalPregnancyDto>(updated);
        }

        public async Task<bool> InactivateAsync(int id)
        {
            var pregnancy = await _repository.GetByIdAsync(id)
                ?? throw new NotFoundException($"Gestação com id '{id}' não encontrada.");

            if (!pregnancy.IsActive)
                throw new ConflictException("A gestação já está inativa.");

            if (await _calvingRepository.HasActiveByPregnancyIdAsync(pregnancy.Id))
                throw new ConflictException("Esta gestação possui um parto ativo vinculado. Inative o parto primeiro.");

            pregnancy.IsActive = false;
            pregnancy.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(pregnancy);
            return true;
        }

        public async Task<bool> CreateForBreedingEventAsync(BreedingEvent breedingEvent, DateTime confirmationDate)
        {
            var pregnancy = new AnimalPregnancy
            {
                AnimalId = breedingEvent.AnimalId,
                BreedingEventId = breedingEvent.Id,
                ConfirmationDate = confirmationDate,
                ExpectedCalvingDate = breedingEvent.BreedingDate.AddDays(GestationDays),
                Status = AnimalPregnancyStatus.Confirmed,
                PropertyId = breedingEvent.PropertyId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            await _repository.CreateAsync(pregnancy);
            return true;
        }
    }
}
