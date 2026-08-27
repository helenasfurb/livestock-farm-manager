using AutoMapper;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Interfaces;
using MuuBoi.Domain.Enums;
using MuuBoi.Domain.Exceptions;
using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Services
{
    public class BreedingEventService : IBreedingEventService
    {
        private readonly IBreedingEventRepository _repository;
        private readonly IAnimalRepository _animalRepository;
        private readonly ISemenSampleRepository _semenSampleRepository;
        private readonly ISemenSampleMovementService _movementService;
        private readonly IAnimalPregnancyService _pregnancyService;
        private readonly IAnimalPregnancyRepository _pregnancyRepository;
        private readonly IMapper _mapper;

        public BreedingEventService(
            IBreedingEventRepository repository,
            IAnimalRepository animalRepository,
            ISemenSampleRepository semenSampleRepository,
            ISemenSampleMovementService movementService,
            IAnimalPregnancyService pregnancyService,
            IAnimalPregnancyRepository pregnancyRepository,
            IMapper mapper)
        {
            _repository = repository;
            _animalRepository = animalRepository;
            _semenSampleRepository = semenSampleRepository;
            _movementService = movementService;
            _pregnancyService = pregnancyService;
            _pregnancyRepository = pregnancyRepository;
            _mapper = mapper;
        }

        public async Task<IEnumerable<BreedingEventListItemDto>> GetByAnimalIdAsync(int animalId)
        {
            _ = await _animalRepository.GetAnimalByIdAsync(animalId)
                ?? throw new NotFoundException($"Animal com id '{animalId}' não encontrado.");

            var events = await _repository.GetByAnimalIdAsync(animalId);
            return _mapper.Map<IEnumerable<BreedingEventListItemDto>>(events);
        }

        public async Task<IEnumerable<BreedingEventListItemDto>> GetAllAsync(BreedingEventFilterDto filter)
        {
            var events = await _repository.GetAllAsync(filter);
            return _mapper.Map<IEnumerable<BreedingEventListItemDto>>(events);
        }

        public async Task<BreedingEventDto> GetByIdAsync(int id)
        {
            var ev = await _repository.GetByIdAsync(id)
                ?? throw new NotFoundException($"Evento reprodutivo com id '{id}' não encontrado.");
            return _mapper.Map<BreedingEventDto>(ev);
        }

        public async Task<BreedingEventDto> CreateAsync(int animalId, BreedingEventCreateDto dto)
        {
            var animal = await _animalRepository.GetAnimalByIdAsync(animalId)
                ?? throw new NotFoundException($"Animal com id '{animalId}' não encontrado.");

            if (!animal.IsActive)
                throw new ConflictException("Não é possível registrar evento reprodutivo para um animal inativo.");

            if (dto.ReproductionType == ReproductionType.ArtificialInsemination)
            {
                var semen = await _semenSampleRepository.GetByIdAsync(dto.SemenSampleId!.Value)
                    ?? throw new NotFoundException($"Amostra de sêmen com id '{dto.SemenSampleId}' não encontrada.");

                if (!semen.IsActive)
                    throw new ConflictException("A amostra de sêmen selecionada está inativa.");

                var availableDoses = await _semenSampleRepository.GetAvailableDosesAsync(semen.Id);
                if (availableDoses <= 0)
                    throw new BusinessRuleException("Não há doses disponíveis para a amostra de sêmen selecionada.");
            }
            else
            {
                var sire = await _animalRepository.GetAnimalByIdAsync(dto.SireAnimalId!.Value)
                    ?? throw new NotFoundException($"Touro com id '{dto.SireAnimalId}' não encontrado.");

                if (!sire.IsActive)
                    throw new ConflictException("O touro selecionado está inativo.");

                if (sire.Classification != AnimalClassification.Bull)
                    throw new BusinessRuleException("O animal informado como touro pai não possui classificação 'Touro'.");
            }

            var ev = _mapper.Map<BreedingEvent>(dto);
            ev.AnimalId = animalId;
            ev.ServiceNumber = await _repository.CountActiveByAnimalIdAsync(animalId) + 1;

            var created = await _repository.CreateAsync(ev);

            if (created.ReproductionType == ReproductionType.ArtificialInsemination)
                await _movementService.CreateForBreedingEventAsync(created);

            created.Animal = animal;
            if (created.SemenSampleId.HasValue)
                created.SemenSample = await _semenSampleRepository.GetByIdAsync(created.SemenSampleId.Value);
            if (created.SireAnimalId.HasValue)
                created.SireAnimal = await _animalRepository.GetAnimalByIdAsync(created.SireAnimalId.Value);

            return _mapper.Map<BreedingEventDto>(created);
        }

        public async Task<BreedingEventDto> UpdateAsync(int id, BreedingEventUpdateDto dto)
        {
            var ev = await _repository.GetByIdAsync(id)
                ?? throw new NotFoundException($"Evento reprodutivo com id '{id}' não encontrado.");

            if (ev.Status != ReproductiveEventStatus.AwaitingDiagnosis)
                throw new ConflictException("Apenas eventos com diagnóstico pendente podem ser editados.");

            if (dto.BreedingDate.HasValue)
                ev.BreedingDate = dto.BreedingDate.Value;

            if (dto.Notes != null)
                ev.Notes = dto.Notes;

            if (ev.ReproductionType == ReproductionType.ArtificialInsemination && dto.SemenSampleId.HasValue)
            {
                var semen = await _semenSampleRepository.GetByIdAsync(dto.SemenSampleId.Value)
                    ?? throw new NotFoundException($"Amostra de sêmen com id '{dto.SemenSampleId}' não encontrada.");

                if (!semen.IsActive)
                    throw new ConflictException("A amostra de sêmen selecionada está inativa.");

                ev.SemenSampleId = dto.SemenSampleId.Value;
            }

            if (ev.ReproductionType == ReproductionType.NaturalMating && dto.SireAnimalId.HasValue)
            {
                var sire = await _animalRepository.GetAnimalByIdAsync(dto.SireAnimalId.Value)
                    ?? throw new NotFoundException($"Touro com id '{dto.SireAnimalId}' não encontrado.");

                if (!sire.IsActive)
                    throw new ConflictException("O touro selecionado está inativo.");

                if (sire.Classification != AnimalClassification.Bull)
                    throw new BusinessRuleException("O animal informado como touro pai não possui classificação 'Touro'.");

                ev.SireAnimalId = dto.SireAnimalId.Value;
            }

            ev.UpdatedAt = DateTime.UtcNow;
            var updated = await _repository.UpdateAsync(ev);

            updated.Animal = await _animalRepository.GetAnimalByIdAsync(ev.AnimalId);
            if (updated.SemenSampleId.HasValue)
                updated.SemenSample = await _semenSampleRepository.GetByIdAsync(updated.SemenSampleId.Value);
            if (updated.SireAnimalId.HasValue)
                updated.SireAnimal = await _animalRepository.GetAnimalByIdAsync(updated.SireAnimalId.Value);

            return _mapper.Map<BreedingEventDto>(updated);
        }

        public async Task<BreedingEventDto> UpdateStatusAsync(int id, BreedingEventStatusUpdateDto dto)
        {
            var ev = await _repository.GetByIdAsync(id)
                ?? throw new NotFoundException($"Evento reprodutivo com id '{id}' não encontrado.");

            if (ev.Status != ReproductiveEventStatus.AwaitingDiagnosis)
                throw new ConflictException("O diagnóstico deste evento já foi registrado.");

            ev.Status = dto.Status;
            ev.DiagnosisDate = dto.DiagnosisDate;
            ev.UpdatedAt = DateTime.UtcNow;

            var updated = await _repository.UpdateAsync(ev);

            if (updated.Status == ReproductiveEventStatus.Successful)
                await _pregnancyService.CreateForBreedingEventAsync(updated, dto.DiagnosisDate);

            updated.Animal = await _animalRepository.GetAnimalByIdAsync(ev.AnimalId);
            if (updated.SemenSampleId.HasValue)
                updated.SemenSample = await _semenSampleRepository.GetByIdAsync(updated.SemenSampleId.Value);
            if (updated.SireAnimalId.HasValue)
                updated.SireAnimal = await _animalRepository.GetAnimalByIdAsync(updated.SireAnimalId.Value);

            return _mapper.Map<BreedingEventDto>(updated);
        }

        public async Task DeactivateAsync(int id)
        {
            var ev = await _repository.GetByIdAsync(id)
                ?? throw new NotFoundException($"Evento reprodutivo com id '{id}' não encontrado.");

            if (!ev.IsActive)
                throw new ConflictException("O evento reprodutivo já está inativo.");

            if (await _pregnancyRepository.ExistsActiveForBreedingEventAsync(ev.Id))
                throw new ConflictException("Este evento possui uma gestação ativa vinculada. Inative a gestação primeiro.");

            ev.IsActive = false;
            ev.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(ev);

            if (ev.ReproductionType == ReproductionType.ArtificialInsemination)
                await _movementService.InactivateForBreedingEventAsync(ev.Id);
        }
    }
}
