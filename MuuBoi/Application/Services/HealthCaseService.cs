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
            if (dto.SyncId.HasValue)
            {
                var existing = await _repository.GetBySyncIdAsync(dto.SyncId.Value);
                if (existing != null)
                    return await GetByIdAsync(existing.Id);
            }

            var animalId = dto.AnimalId!.Value;
            var animal = await _animalRepository.GetAnimalByIdAsync(animalId)
                ?? throw new NotFoundException($"Animal com id '{animalId}' não encontrado.");

            if (dto.DiseaseType == DiseaseType.Mastitis)
                EnsureMastitisEligible(animal);

            var healthCase = new HealthCase
            {
                SyncId = dto.SyncId ?? Guid.NewGuid(),
                AnimalId = animalId,
                DiseaseType = dto.DiseaseType,
                DiseaseName = dto.DiseaseType == DiseaseType.Other ? dto.DiseaseName : null,
                DiagnosisDate = dto.DiagnosisDate!.Value,
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

            var editedAt = SyncTimestampResolver.ResolveEditedAt(dto.UpdatedAt, DateTime.UtcNow);
            if (SyncTimestampResolver.IsOutdated(editedAt, healthCase))
                return BuildDetail(healthCase, DateTime.UtcNow);

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

            healthCase.UpdatedAt = editedAt;
            await _repository.UpdateAsync(healthCase);

            return await GetByIdAsync(id);
        }

        public async Task<bool> DeactivateAsync(int id)
        {
            var healthCase = await _repository.GetByIdAsync(id)
                ?? throw new NotFoundException($"Caso de saúde com id '{id}' não encontrado.");

            if (!healthCase.IsActive)
                return true;

            healthCase.IsActive = false;
            await _repository.UpdateAsync(healthCase);
            return true;
        }

        public async Task<MedicationUseDto> AddMedicationAsync(int healthCaseId, MedicationUseCreateDto dto)
        {
            if (dto.SyncId.HasValue)
            {
                var existing = await _repository.GetMedicationBySyncIdAsync(dto.SyncId.Value);
                if (existing != null)
                    return BuildMedicationUse(existing);
            }

            var healthCase = await _repository.GetByIdAsync(healthCaseId)
                ?? throw new NotFoundException($"Caso de saúde com id '{healthCaseId}' não encontrado.");

            var application = new AnimalMedication
            {
                SyncId = dto.SyncId ?? Guid.NewGuid(),
                AnimalId = healthCase.AnimalId,
                HealthCaseId = healthCase.Id,
                MedicationName = dto.MedicationName,
                ApplicationDate = dto.ApplicationDate!.Value,
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
                return true;

            medication.IsActive = false;
            await _repository.UpdateMedicationAsync(medication);
            return true;
        }

        public async Task<MastitisTestDto> AddTestAsync(int healthCaseId, MastitisTestCreateDto dto)
        {
            if (dto.SyncId.HasValue)
            {
                var existing = await _repository.GetTestBySyncIdAsync(dto.SyncId.Value);
                if (existing != null)
                    return _mapper.Map<MastitisTestDto>(existing);
            }

            var healthCase = await _repository.GetByIdAsync(healthCaseId)
                ?? throw new NotFoundException($"Caso de saúde com id '{healthCaseId}' não encontrado.");

            var test = new MastitisTest
            {
                SyncId = dto.SyncId ?? Guid.NewGuid(),
                HealthCaseId = healthCase.Id,
                TestType = dto.TestType,
                Result = dto.Result,
                TestDate = dto.TestDate!.Value
            };

            var created = await _repository.AddTestAsync(test);
            return _mapper.Map<MastitisTestDto>(created);
        }

        public async Task<bool> DeactivateTestAsync(int healthCaseId, int testId)
        {
            var test = await _repository.GetTestAsync(testId, healthCaseId)
                ?? throw new NotFoundException($"Teste com id '{testId}' não encontrado.");

            if (!test.IsActive)
                return true;

            test.IsActive = false;
            await _repository.UpdateTestAsync(test);
            return true;
        }

        public async Task<SyncPageDto<HealthCaseSyncDto>> GetChangesAsync(string? since, int? limit)
        {
            if (!SyncPaging.TryDecodeCursor(since, out var cursor))
                throw new ValidationException("Cursor de sincronização inválido.");

            var take = SyncPaging.ResolveLimit(limit);
            var fetched = await _repository.GetChangesAsync(cursor, take + 1);
            var caseIds = fetched.Select(c => c.Id).ToList();
            var medications = (await _repository.GetMedicationsByCaseIdsAsync(caseIds)).ToLookup(m => m.HealthCaseId!.Value);
            var tests = (await _repository.GetTestsByCaseIdsAsync(caseIds)).ToLookup(t => t.HealthCaseId);

            return SyncPaging.BuildPage(fetched, take, cursor, c =>
            {
                var dto = _mapper.Map<HealthCaseSyncDto>(c);
                dto.Medications = _mapper.Map<List<MedicationUseSyncDto>>(medications[c.Id]);
                dto.Tests = _mapper.Map<List<MastitisTestSyncDto>>(tests[c.Id]);
                return dto;
            });
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

        private static void EnsureMastitisEligible(Animal animal)
        {
            if (animal.Gender == AnimalGender.M ||
                animal.Classification == AnimalClassification.Bull ||
                animal.Classification == AnimalClassification.Steer)
                throw new BusinessRuleException("Não é possível registrar mastite para machos (touro/boi).");
        }

        private async Task EnsureAnimalExistsAsync(int animalId)
        {
            _ = await _animalRepository.GetAnimalByIdAsync(animalId)
                ?? throw new NotFoundException($"Animal com id '{animalId}' não encontrado.");
        }
    }
}
