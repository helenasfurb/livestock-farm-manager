using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Domain.Models
{
    public class Breed : BaseEntity, ITenantEntity
    {
        [Required(ErrorMessage = "Name is required")]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public Guid PropertyId { get; set; }

        public ICollection<Animal>? Animals { get; set; }
    }
}
