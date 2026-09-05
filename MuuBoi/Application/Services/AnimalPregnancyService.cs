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
        private readonly ISemenSampleRepository _semenSampleRepository;
        private readonly IMapper _mapper;

        public AnimalPregnancyService(
            IAnimalPregnancyRepository repository,
            IAnimalCalvingRepository calvingRepository,
            IAnimalRepository animalRepository,
            ISemenSampleRepository semenSampleRepository,
            IMapper mapper)
        {
            _repository = repository;
            _calvingRepository = calvingRepository;
            _animalRepository = animalRepository;
            _semenSampleRepository = semenSampleRepository;
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

        public async Task<AnimalPregnancyDto> CreateRetroactiveAsync(int animalId, AnimalPregnancyRetroactiveCreateDto dto)
        {
            // RN-07: idempotência — reenvio com o mesmo ClientRequestId devolve a gestação já criada.
            if (dto.ClientRequestId.HasValue)
            {
                var existing = await _repository.GetByClientRequestIdAsync(dto.ClientRequestId.Value);
                if (existing != null)
                    return _mapper.Map<AnimalPregnancyDto>(existing);
            }

            var animal = await _animalRepository.GetAnimalByIdAsync(animalId)
                ?? throw new NotFoundException($"Animal com id '{animalId}' não encontrado.");

            // RN-01: fêmea Cow/Heifer ativa.
            if (!animal.IsActive)
                throw new ConflictException("Não é possível registrar gestações para um animal inativo.");

            if (animal.Classification != AnimalClassification.Cow &&
                animal.Classification != AnimalClassification.Heifer)
                throw new BusinessRuleException("Apenas vacas e novilhas podem ter gestação registrada.");

            // RN-02: não empilhar gestação ativa confirmada.
            if (await _repository.HasActiveConfirmedByAnimalIdAsync(animalId))
                throw new ConflictException("O animal já possui uma gestação ativa confirmada.");

            // RN-04: no máximo um entre touro e sêmen (também validado no DTO).
            if (dto.SireAnimalId.HasValue && dto.SemenSampleId.HasValue)
                throw new BusinessRuleException("Informe no máximo um entre touro e sêmen.");

            Animal? sire = null;
            SemenSample? semen = null;

            // RN-05: se informado, o pai deve ser válido e ativo.
            if (dto.SireAnimalId.HasValue)
            {
                sire = await _animalRepository.GetAnimalByIdAsync(dto.SireAnimalId.Value)
                    ?? throw new NotFoundException($"Touro com id '{dto.SireAnimalId}' não encontrado.");

                if (!sire.IsActive)
                    throw new ConflictException("O touro informado está inativo.");

                if (sire.Classification != AnimalClassification.Bull)
                    throw new BusinessRuleException("O animal informado como pai não possui classificação 'Touro'.");
            }

            if (dto.SemenSampleId.HasValue)
            {
                semen = await _semenSampleRepository.GetByIdAsync(dto.SemenSampleId.Value)
                    ?? throw new NotFoundException($"Amostra de sêmen com id '{dto.SemenSampleId}' não encontrada.");

                if (!semen.IsActive)
                    throw new ConflictException("A amostra de sêmen informada está inativa.");
            }

            // RN-03: data prevista de parto — informada direto prevalece; senão calcula da concepção.
            var expectedCalvingDate = dto.ExpectedCalvingDate
                ?? dto.EstimatedConceptionDate!.Value.AddDays(GestationDays);

            if (expectedCalvingDate <= dto.ConfirmationDate)
                throw new BusinessRuleException("A data prevista de parto deve ser posterior à data de confirmação.");

            var pregnancy = new AnimalPregnancy
            {
                AnimalId = animalId,
                BreedingEventId = null,
                SireAnimalId = dto.SireAnimalId,
                SemenSampleId = dto.SemenSampleId,
                ConfirmationDate = dto.ConfirmationDate,
                ExpectedCalvingDate = expectedCalvingDate,
                Status = AnimalPregnancyStatus.Confirmed,
                Notes = dto.Notes,
                ClientRequestId = dto.ClientRequestId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            // RN-06: gestação retroativa não consome dose de sêmen — vínculo só genealógico.

            var created = await _repository.CreateAsync(pregnancy);
            created.Animal = animal;
            created.SireAnimal = sire;
            created.SemenSample = semen;

            return _mapper.Map<AnimalPregnancyDto>(created);
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
