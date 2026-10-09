using System.ComponentModel.DataAnnotations;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Application.DTOs
{
    public class SemenSampleCreateDto : IValidatableObject
    {
        public Guid? SyncId { get; set; }

        [Required(ErrorMessage = "O nome é obrigatório.")]
        [MaxLength(100, ErrorMessage = "O nome deve ter no máximo 100 caracteres.")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(100, ErrorMessage = "O registro do touro deve ter no máximo 100 caracteres.")]
        public string? BullRegistration { get; set; }

        [MaxLength(200, ErrorMessage = "A empresa de genética deve ter no máximo 200 caracteres.")]
        public string? GeneticsCompany { get; set; }

        public AnimalBreed? BullBreed { get; set; }

        [MaxLength(100, ErrorMessage = "O número do lote deve ter no máximo 100 caracteres.")]
        public string? BatchNumber { get; set; }

        [MaxLength(500, ErrorMessage = "As observações devem ter no máximo 500 caracteres.")]
        public string? Notes { get; set; }

        [Range(1, 9999, ErrorMessage = "A quantidade inicial deve ser entre 1 e 9.999.")]
        public int? InitialQuantity { get; set; }

        [MaxLength(500, ErrorMessage = "As observações iniciais devem ter no máximo 500 caracteres.")]
        public string? InitialNotes { get; set; }

        public Guid? InitialMovementSyncId { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (SyncId.HasValue && SyncId.Value == Guid.Empty)
                yield return new ValidationResult(
                    "O identificador de sincronização não pode ser vazio.",
                    new[] { nameof(SyncId) });

            if (InitialMovementSyncId.HasValue && InitialMovementSyncId.Value == Guid.Empty)
                yield return new ValidationResult(
                    "O identificador de sincronização da entrada inicial não pode ser vazio.",
                    new[] { nameof(InitialMovementSyncId) });
        }
    }
}
