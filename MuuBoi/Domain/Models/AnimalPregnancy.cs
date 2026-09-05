using System.ComponentModel.DataAnnotations;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Domain.Models
{
    public class AnimalPregnancy : BaseEntity, ITenantEntity
    {
        public int AnimalId { get; set; }

        // Nulável: gestação retroativa (Spec #13) não tem cobertura vinculada.
        public int? BreedingEventId { get; set; }

        // Pai capturado no cadastro retroativo (CU-C). Mutuamente exclusivos e opcionais;
        // vínculo apenas genealógico — não consome dose de sêmen (RN-06).
        public int? SireAnimalId { get; set; }

        public int? SemenSampleId { get; set; }

        public DateTime ConfirmationDate { get; set; }

        public DateTime ExpectedCalvingDate { get; set; }

        public DateTime? LossDate { get; set; }

        public AnimalPregnancyStatus Status { get; set; } = AnimalPregnancyStatus.Confirmed;

        [MaxLength(500)]
        public string? Notes { get; set; }

        // Chave de idempotência do cadastro (RN-07 / gancho da Spec #14).
        public Guid? ClientRequestId { get; set; }

        public Guid PropertyId { get; set; }

        public Animal? Animal { get; set; }
        public BreedingEvent? BreedingEvent { get; set; }
        public Animal? SireAnimal { get; set; }
        public SemenSample? SemenSample { get; set; }
        public ICollection<AnimalCalving>? Calvings { get; set; }
    }
}
