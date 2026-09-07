using AutoMapper;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Helpers;
using MuuBoi.Application.Interfaces;
using MuuBoi.Domain.Enums;
using MuuBoi.Domain.Exceptions;
using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Services
{
    public class HealthCaseService : IHealthCaseService
    {
        private readonly IHealthCaseRepository _repository;
        private readonly IAnimalRepository _animalRepository;
        private readonly IMapper _mapper;

        public HealthCaseService(
            IHealthCaseRepository repository,
            IAnimalRepository animalRepository,
            IMapper mapper)
        {
            _repository = repository;
            _animalRepository = animalRepository;
            _mapper = mapper;
        }

        public async Task<IEnumerable<HealthCaseListItemDto>> GetAllAsync(HealthCaseFilterDto filter)
        {
            var cases = await _repository.GetAllAsync(filter);
            var now = DateTime.UtcNow;

            var items = cases.Select(c => BuildListItem(c, now));

            if (filter.Status.HasValue)
                items = items.Where(i => i.Status!.Value == (int)filter.Status.Value);

            return items.ToList();
        }

        public async Task<HealthCaseDto> GetByIdAsync(int id)
        {
            var healthCase = await _repository.GetByIdAsync(id)
                ?? throw new NotFoundException($"Caso de saúde com id '{id}' não encontrado.");

            return BuildDetail(healthCase, DateTime.UtcNow);
        }

        public async Task<HealthCaseDto> CreateAsync(HealthCaseCreateDto dto)
        {
            await EnsureAnimalExistsAsync(dto.AnimalId);

            var healthCase = new HealthCase
            {
                AnimalId = dto.AnimalId,
                DiseaseType = dto.DiseaseType,
                DiseaseName = dto.DiseaseType == DiseaseType.Other ? dto.DiseaseName : null,
                DiagnosisDate = dto.DiagnosisDate,
                AffectedQuarters = dto.DiseaseType == DiseaseType.Mastitis ? dto.AffectedQuarters : null,
                Notes = dto.Notes
            };

            var created = await _repository.CreateAsync(healthCase);
            return await GetByIdAsync(created.Id);
        }

        public async Task<HealthCaseDto> UpdateAsync(int id, HealthCaseUpdateDto dto)
        {
            var healthCase = await _repository.GetByIdAsync(id)
                ?? throw new NotFoundException($"Caso de saúde com id '{id}' não encontrado.");

            if (dto.DiseaseName != null && healthCase.DiseaseType == DiseaseType.Other)
                healthCase.DiseaseName = dto.DiseaseName;

            if (dto.DiagnosisDate.HasValue)
                healthCase.DiagnosisDate = dto.DiagnosisDate.Value;

            if (dto.AffectedQuarters.HasValue && healthCase.DiseaseType == DiseaseType.Mastitis)
                healthCase.AffectedQuarters = dto.AffectedQuarters.Value;

            if (dto.ResolvedAt.HasValue)
                healthCase.ResolvedAt = dto.ResolvedAt.Value;

            if (dto.Notes != null)
                healthCase.Notes = dto.Notes;

            healthCase.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(healthCase);

            return await GetByIdAsync(id);
        }

        public async Task<bool> DeactivateAsync(int id)
        {
            var healthCase = await _repository.GetByIdAsync(id)
                ?? throw new NotFoundException($"Caso de saúde com id '{id}' não encontrado.");

            if (!healthCase.IsActive)
                throw new ConflictException("O caso de saúde já está inativo.");

            healthCase.IsActive = false;
            healthCase.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(healthCase);
            return true;
        }

        public async Task<MedicationUseDto> AddMedicationAsync(int healthCaseId, MedicationUseCreateDto dto)
        {
            var healthCase = await _repository.GetByIdAsync(healthCaseId)
                ?? throw new NotFoundException($"Caso de saúde com id '{healthCaseId}' não encontrado.");

            var application = new AnimalMedication
            {
                AnimalId = healthCase.AnimalId,
                HealthCaseId = healthCase.Id,
                MedicationName = dto.MedicationName,
                ApplicationDate = dto.ApplicationDate,
                WithdrawalPeriodDays = dto.WithdrawalPeriodDays ?? 0,
                DosageDescription = dto.Dose,
                Responsible = dto.Responsible
            };

            var created = await _repository.AddMedicationAsync(application);
            var withMedication = await _repository.GetMedicationAsync(created.Id, healthCase.Id);
            return BuildMedicationUse(withMedication!);
        }

        public async Task<bool> DeactivateMedicationAsync(int healthCaseId, int medicationId)
        {
            var medication = await _repository.GetMedicationAsync(medicationId, healthCaseId)
                ?? throw new NotFoundException($"Aplicação com id '{medicationId}' não encontrada.");

            if (!medication.IsActive)
                throw new ConflictException("A aplicação já está inativa.");

            medication.IsActive = false;
            medication.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateMedicationAsync(medication);
            return true;
        }

        public async Task<MastitisTestDto> AddTestAsync(int healthCaseId, MastitisTestCreateDto dto)
        {
            var healthCase = await _repository.GetByIdAsync(healthCaseId)
                ?? throw new NotFoundException($"Caso de saúde com id '{healthCaseId}' não encontrado.");

            var test = new MastitisTest
            {
                HealthCaseId = healthCase.Id,
                TestType = dto.TestType,
                Result = dto.Result,
                TestDate = dto.TestDate
            };

            var created = await _repository.AddTestAsync(test);
            return _mapper.Map<MastitisTestDto>(created);
        }

        public async Task<bool> DeactivateTestAsync(int healthCaseId, int testId)
        {
            var test = await _repository.GetTestAsync(testId, healthCaseId)
                ?? throw new NotFoundException($"Teste com id '{testId}' não encontrado.");

            if (!test.IsActive)
                throw new ConflictException("O teste já está inativo.");

            test.IsActive = false;
            test.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateTestAsync(test);
            return true;
        }

        public async Task<IEnumerable<HealthCaseListItemDto>> GetAnimalHistoryAsync(int animalId)
        {
            await EnsureAnimalExistsAsync(animalId);
            var cases = await _repository.GetByAnimalAsync(animalId);
            var now = DateTime.UtcNow;
            return cases.Select(c => BuildListItem(c, now)).ToList();
        }

        public async Task<Dictionary<int, AnimalSanitaryStatusDto>> GetSanitaryStatusMapAsync(
            IReadOnlyCollection<int> animalIds)
        {
            var result = new Dictionary<int, AnimalSanitaryStatusDto>();
            if (animalIds == null || animalIds.Count == 0)
                return result;

            var facts = await _repository.GetSanitaryFactsMapAsync(animalIds, DateTime.UtcNow);

            foreach (var (animalId, f) in facts)
            {
                var status = AnimalSanitaryStatusResolver.Resolve(
                    f.HasCaseUnderTreatment, f.MilkWithheldUntil, f.HasSuspectedCase);
                result[animalId] = new AnimalSanitaryStatusDto
                {
                    Status = new EnumValueDto { Value = (int)status, Label = status.GetDescription() },
                    MilkWithheldUntil = f.MilkWithheldUntil
                };
            }

            return result;
        }

        // ----- builders -----

        private HealthCaseListItemDto BuildListItem(HealthCase c, DateTime now)
        {
            var dto = _mapper.Map<HealthCaseListItemDto>(c);
            var liberation = CaseLiberationDate(c);
            dto.Status = StatusOf(c, liberation, now);
            dto.MilkLiberationDate = liberation;
            dto.AffectedQuarters = DecomposeQuarters(c.AffectedQuarters);
            return dto;
        }

        private HealthCaseDto BuildDetail(HealthCase c, DateTime now)
        {
            var dto = _mapper.Map<HealthCaseDto>(c);
            var liberation = CaseLiberationDate(c);
            dto.Status = StatusOf(c, liberation, now);
            dto.CaseLiberationDate = liberation;
            dto.AffectedQuarters = DecomposeQuarters(c.AffectedQuarters);
            dto.Medications = ActiveMedications(c).Select(BuildMedicationUse).ToList();
            dto.Tests = ActiveTests(c).Select(t => _mapper.Map<MastitisTestDto>(t)).ToList();
            return dto;
        }

        private MedicationUseDto BuildMedicationUse(AnimalMedication m)
        {
            var dto = _mapper.Map<MedicationUseDto>(m);
            dto.MilkLiberationDate = m.WithdrawalPeriodDays.HasValue
                ? m.ApplicationDate.Date.AddDays(m.WithdrawalPeriodDays.Value)
                : null;
            return dto;
        }

        private static EnumValueDto StatusOf(HealthCase c, DateTime? liberation, DateTime now)
        {
            var hasMedication = ActiveMedications(c).Any();
            var status = HealthCaseStatusResolver.Resolve(hasMedication, c.ResolvedAt, liberation, now);
            return new EnumValueDto { Value = (int)status, Label = status.GetDescription() };
        }

        private static DateTime? CaseLiberationDate(HealthCase c)
        {
            var liberations = ActiveMedications(c)
                .Select(m => m.ApplicationDate.Date.AddDays(m.WithdrawalPeriodDays ?? 0))
                .ToList();
            return liberations.Count > 0 ? liberations.Max() : null;
        }

        private static IEnumerable<AnimalMedication> ActiveMedications(HealthCase c)
            => c.Medications?.Where(m => m.IsActive) ?? Enumerable.Empty<AnimalMedication>();

        private static IEnumerable<MastitisTest> ActiveTests(HealthCase c)
            => c.Tests?.Where(t => t.IsActive) ?? Enumerable.Empty<MastitisTest>();

        private static IEnumerable<EnumValueDto> DecomposeQuarters(Quarter? quarters)
            => quarters.HasValue
                ? EnumHelper.ToFlagValues(quarters.Value)
                : new List<EnumValueDto>();

        private async Task EnsureAnimalExistsAsync(int animalId)
        {
            _ = await _animalRepository.GetAnimalByIdAsync(animalId)
                ?? throw new NotFoundException($"Animal com id '{animalId}' não encontrado.");
        }
    }
}
