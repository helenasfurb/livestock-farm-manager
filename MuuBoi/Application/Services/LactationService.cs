using System.ComponentModel.DataAnnotations;
using AutoMapper;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Helpers;
using MuuBoi.Application.Interfaces;
using MuuBoi.Domain.Enums;
using MuuBoi.Domain.Exceptions;
using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Services
{
    public class LactationService : ILactationService
    {
        private readonly ILactationRepository _repository;
        private readonly IAnimalRepository _animalRepository;
        private readonly IMapper _mapper;

        public LactationService(
            ILactationRepository repository,
            IAnimalRepository animalRepository,
            IMapper mapper)
        {
            _repository = repository;
            _animalRepository = animalRepository;
            _mapper = mapper;
        }

        public async Task<IEnumerable<LactationListItemDto>> GetByAnimalIdAsync(int animalId)
        {
            await FindAnimalAsync(animalId);

            var lactations = await _repository.GetByAnimalIdAsync(animalId);
            return lactations.Select(ToListItem).ToList();
        }

        public async Task<LactationDto?> GetCurrentByAnimalIdAsync(int animalId)
        {
            await FindAnimalAsync(animalId);

            var lactation = await _repository.GetOpenByAnimalIdAsync(animalId);
            return lactation == null ? null : ToDto(lactation);
        }

        public async Task<LactationDto> GetByIdAsync(int id)
        {
            var lactation = await _repository.GetByIdAsync(id)
                ?? throw new NotFoundException($"Lactação com id '{id}' não encontrada.");
            return ToDto(lactation);
        }

        public async Task<LactationDto> CreateAsync(int animalId, LactationCreateDto dto)
        {
            if (dto.SyncId.HasValue)
            {
                var existing = await _repository.GetBySyncIdAsync(dto.SyncId.Value);
                if (existing != null)
                    return ToDto(existing);
            }

            var animal = await FindAnimalAsync(animalId);

            if (!animal.IsActive)
                throw new ConflictException("Não é possível cadastrar lactação para um animal inativo.");

            EnsureLactatingClassification(animal);

            if (await _repository.HasOpenByAnimalIdAsync(animalId))
            {
                if (dto.SyncId.HasValue)
                {
                    var concurrent = await _repository.GetBySyncIdAsync(dto.SyncId.Value);
                    if (concurrent != null)
                        return ToDto(concurrent);
                }

                throw new ConflictException("O animal já possui uma lactação em aberto. Seque a atual antes de abrir outra.");
            }

            var lactation = new Lactation
            {
                SyncId = dto.SyncId ?? Guid.NewGuid(),
                AnimalId = animalId,
                StartDate = dto.StartDate!.Value,
                EndDate = null,
                CalvingId = null,
                Origin = LactationOrigin.InitialSeed,
                PropertyId = animal.PropertyId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var created = await _repository.CreateAsync(lactation);
            created.Animal = animal;
            return ToDto(created);
        }

        public async Task<LactationDto> UpdateAsync(int id, LactationUpdateDto dto)
        {
            var lactation = await _repository.GetByIdAsync(id)
                ?? throw new NotFoundException($"Lactação com id '{id}' não encontrada.");

            var editedAt = SyncTimestampResolver.ResolveEditedAt(dto.UpdatedAt, DateTime.UtcNow);
            if (!lactation.IsActive || SyncTimestampResolver.IsOutdated(editedAt, lactation))
                return ToDto(lactation);

            if (dto.StartDate.HasValue)
            {
                if (lactation.EndDate.HasValue && dto.StartDate.Value > lactation.EndDate.Value)
                    throw new BusinessRuleException("A data de início não pode ser posterior à data da secagem.");

                lactation.StartDate = dto.StartDate.Value;
            }

            lactation.UpdatedAt = editedAt;
            var updated = await _repository.UpdateAsync(lactation);
            return ToDto(updated);
        }

        public async Task<LactationDto> DryOffAsync(int id, LactationDryOffDto dto)
        {
            var lactation = await _repository.GetByIdAsync(id)
                ?? throw new NotFoundException($"Lactação com id '{id}' não encontrada.");

            if (!lactation.IsActive || lactation.EndDate.HasValue)
                return ToDto(lactation);

            if (dto.EndDate!.Value < lactation.StartDate)
                throw new BusinessRuleException("A data da secagem não pode ser anterior ao início da lactação.");

            lactation.EndDate = dto.EndDate.Value;
            lactation.DryOffNotes = dto.DryOffNotes;

            var updated = await _repository.UpdateAsync(lactation);
            return ToDto(updated);
        }

        public async Task<LactationDto> UndoDryOffAsync(int id)
        {
            var lactation = await _repository.GetByIdAsync(id)
                ?? throw new NotFoundException($"Lactação com id '{id}' não encontrada.");

            if (!lactation.IsActive || lactation.EndDate == null)
                return ToDto(lactation);

            if (await _repository.HasOpenByAnimalIdAsync(lactation.AnimalId))
                throw new ConflictException("O animal já possui outra lactação em aberto.");

            lactation.EndDate = null;
            lactation.DryOffNotes = null;

            var updated = await _repository.UpdateAsync(lactation);
            return ToDto(updated);
        }

        public async Task<bool> DeactivateAsync(int id)
        {
            var lactation = await _repository.GetByIdAsync(id)
                ?? throw new NotFoundException($"Lactação com id '{id}' não encontrada.");

            if (!lactation.IsActive)
                return true;

            lactation.IsActive = false;
            await _repository.UpdateAsync(lactation);
            return true;
        }

        public async Task<SyncPageDto<LactationDto>> GetChangesAsync(string? since, int? limit)
        {
            if (!SyncPaging.TryDecodeCursor(since, out var cursor))
                throw new ValidationException("Cursor de sincronização inválido.");

            var take = SyncPaging.ResolveLimit(limit);
            var fetched = await _repository.GetChangesAsync(cursor, take + 1);
            var tags = await _animalRepository.GetTagNumbersByIdsAsync(fetched.Select(l => l.AnimalId).Distinct().ToList());

            return SyncPaging.BuildPage(fetched, take, cursor, l =>
            {
                var dto = ToDto(l);
                dto.AnimalTagNumber = tags.GetValueOrDefault(l.AnimalId);
                return dto;
            });
        }

        private async Task<Animal> FindAnimalAsync(int animalId)
        {
            return await _animalRepository.GetAnimalByIdAsync(animalId)
                ?? throw new NotFoundException($"Animal com id '{animalId}' não encontrado.");
        }

        private static void EnsureLactatingClassification(Animal animal)
        {
            if (animal.Classification != AnimalClassification.Cow &&
                animal.Classification != AnimalClassification.Heifer)
                throw new BusinessRuleException("Somente vacas e novilhas podem ter lactação.");
        }

        private LactationDto ToDto(Lactation lactation)
        {
            var dto = _mapper.Map<LactationDto>(lactation);
            var now = DateTime.UtcNow;
            dto.IsLactating = ProductiveStatusResolver.IsLactating(lactation.StartDate, lactation.EndDate, now);
            dto.DaysInMilk = ProductiveStatusResolver.DaysInMilk(lactation.StartDate, lactation.EndDate, now);
            return dto;
        }

        private LactationListItemDto ToListItem(Lactation lactation)
        {
            var dto = _mapper.Map<LactationListItemDto>(lactation);
            var now = DateTime.UtcNow;
            dto.IsLactating = ProductiveStatusResolver.IsLactating(lactation.StartDate, lactation.EndDate, now);
            dto.DaysInMilk = ProductiveStatusResolver.DaysInMilk(lactation.StartDate, lactation.EndDate, now);
            return dto;
        }
    }
}
