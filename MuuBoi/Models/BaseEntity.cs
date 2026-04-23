using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Models
{
    public abstract class BaseEntity
    {
        [Key]
        public int Id { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}
