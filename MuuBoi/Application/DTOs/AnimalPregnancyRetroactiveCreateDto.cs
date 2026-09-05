using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Application.DTOs
{
    /// <summary>
    /// Cadastro retroativo de gestação (Spec #13) — sem cobertura vinculada.
    /// Usado no onboarding (vaca já prenhe no go-live) e na compra de vaca gestante.
    /// </summary>
    public class AnimalPregnancyRetroactiveCreateDto : IValidatableObject
    {
        [Required(ErrorMessage = "A data de confirmação é obrigatória.")]
        public DateTime ConfirmationDate { get; set; }

        /// <summary>
        /// Data estimada de concepção. Se a data prevista de parto não for informada,
        /// o serviço a calcula a partir desta (concepção + duração média de gestação).
        /// Pelo menos uma das duas datas deve ser informada.
        /// </summary>
        public DateTime? EstimatedConceptionDate { get; set; }

        /// <summary>
        /// Data prevista de parto informada diretamente. Prevalece sobre o cálculo por
        /// concepção quando ambas são informadas. Pelo menos uma das duas datas deve ser informada.
        /// </summary>
        public DateTime? ExpectedCalvingDate { get; set; }

        /// <summary>Touro do rebanho, se conhecido (CU-C). Mutuamente exclusivo com <see cref="SemenSampleId"/>.</summary>
        public int? SireAnimalId { get; set; }

        /// <summary>Amostra de sêmen, se conhecida (CU-C). Mutuamente exclusiva com <see cref="SireAnimalId"/>.</summary>
        public int? SemenSampleId { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        /// <summary>Chave de idempotência (RN-07): reenvio com o mesmo valor não cria gestação duplicada.</summary>
        public Guid? ClientRequestId { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            // RN-03: pelo menos uma das datas deve ser informada (podem coexistir).
            if (!EstimatedConceptionDate.HasValue && !ExpectedCalvingDate.HasValue)
                yield return new ValidationResult(
                    "Informe pelo menos uma: data estimada de concepção ou data prevista de parto.",
                    new[] { nameof(EstimatedConceptionDate), nameof(ExpectedCalvingDate) });

            // RN-03: se a data prevista foi informada direto, deve ser posterior à confirmação.
            if (ExpectedCalvingDate.HasValue && ExpectedCalvingDate.Value <= ConfirmationDate)
                yield return new ValidationResult(
                    "A data prevista de parto deve ser posterior à data de confirmação.",
                    new[] { nameof(ExpectedCalvingDate) });

            // RN-04: no máximo um entre touro e sêmen.
            if (SireAnimalId.HasValue && SemenSampleId.HasValue)
                yield return new ValidationResult(
                    "Informe no máximo um entre touro (SireAnimalId) e sêmen (SemenSampleId).",
                    new[] { nameof(SireAnimalId), nameof(SemenSampleId) });

            if (ConfirmationDate > DateTime.UtcNow)
                yield return new ValidationResult(
                    "A data de confirmação não pode ser futura.",
                    new[] { nameof(ConfirmationDate) });
        }
    }
}
