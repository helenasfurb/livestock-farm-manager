using AutoMapper;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Interfaces;
using MuuBoi.Domain.Enums;
using MuuBoi.Domain.Exceptions;
using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Services
{
    public class AnimalCalvingService : IAnimalCalvingService
    {
        private readonly IAnimalCalvingRepository _repository;
        private readonly IAnimalPregnancyRepository _pregnancyRepository;
        private readonly IBodyConditionRecordService _bodyConditionRecordService;
        private readonly IMapper _mapper;

        public AnimalCalvingService(
            IAnimalCalvingRepository repository,
            IAnimalPregnancyRepository pregnancyRepository,
            IBodyConditionRecordService bodyConditionRecordService,
            IMapper mapper)
        {
            _repository = repository;
            _pregnancyRepository = pregnancyRepository;
            _bodyConditionRecordService = bodyConditionRecordService;
            _mapper = mapper;
        }

        public async Task<AnimalCalvingDto> CreateAsync(int pregnancyId, AnimalCalvingCreateDto dto)
        {
            var pregnancy = await _pregnancyRepository.GetByIdAsync(pregnancyId)
                ?? throw new NotFoundException($"Gestação com id '{pregnancyId}' não encontrada.");

            if (pregnancy.Status != AnimalPregnancyStatus.Confirmed)
                throw new ConflictException("O parto só pode ser registrado para uma gestação confirmada.");

            if (await _repository.HasActiveByPregnancyIdAsync(pregnancy.Id))
                throw new ConflictException("Esta gestação já possui um parto ativo registrado.");

            if (dto.CalvingDate < pregnancy.ConfirmationDate)
                throw new BusinessRuleException("A data do parto não pode ser anterior à data de confirmação da gestação.");

            var calving = new AnimalCalving
            {
                AnimalPregnancyId = pregnancy.Id,
                AnimalId = pregnancy.AnimalId,
                CalvingDate = dto.CalvingDate,
                Notes = dto.Notes,
                PropertyId = pregnancy.PropertyId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                Calves = dto.Calves.Select(calfDto =>
                {
                    var calf = _mapper.Map<AnimalCalvingCalf>(calfDto);
                    calf.PropertyId = pregnancy.PropertyId;
                    return calf;
                }).ToList()
            };

            var created = await _repository.CreateAsync(calving);

            pregnancy.Status = AnimalPregnancyStatus.Calved;
            pregnancy.UpdatedAt = DateTime.UtcNow;
            await _pregnancyRepository.UpdateAsync(pregnancy);

            if (dto.BodyConditionScore.HasValue)
                await _bodyConditionRecordService.CreateAsync(pregnancy.AnimalId, new BodyConditionRecordCreateDto
                {
                    Score = dto.BodyConditionScore.Value,
                    RecordedAt = dto.CalvingDate
                });

            return _mapper.Map<AnimalCalvingDto>(created);
        }

        public async Task<bool> InactivateAsync(int id)
        {
            var calving = await _repository.GetByIdAsync(id)
                ?? throw new NotFoundException($"Parto com id '{id}' não encontrado.");

            if (!calving.IsActive)
                throw new ConflictException("O parto já está inativo.");

            calving.IsActive = false;
            calving.UpdatedAt = DateTime.UtcNow;

            if (calving.Calves != null)
                foreach (var calf in calving.Calves.Where(c => c.IsActive))
                {
                    calf.IsActive = false;
                    calf.UpdatedAt = DateTime.UtcNow;
                }

            await _repository.UpdateAsync(calving);

            if (calving.AnimalPregnancy != null)
            {
                calving.AnimalPregnancy.Status = AnimalPregnancyStatus.Confirmed;
                calving.AnimalPregnancy.UpdatedAt = DateTime.UtcNow;
                await _pregnancyRepository.UpdateAsync(calving.AnimalPregnancy);
            }

            return true;
        }
    }
}
