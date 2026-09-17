using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Domain.Models
{
    public class AnimalCalving : BaseEntity, ITenantEntity
    {
        public int AnimalPregnancyId { get; set; }

        public int AnimalId { get; set; }

        public DateTime CalvingDate { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public Guid PropertyId { get; set; }

        public AnimalPregnancy? AnimalPregnancy { get; set; }
        public Animal? Animal { get; set; }
        public ICollection<AnimalCalvingCalf>? Calves { get; set; }
    }
}
