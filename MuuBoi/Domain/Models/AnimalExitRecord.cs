using System.ComponentModel.DataAnnotations;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Domain.Models
{
    public class AnimalExitRecord : BaseEntity
    {
        public int AnimalId { get; set; }

        public AnimalExitReason ExitReason { get; set; }

        public DateTime ExitDate { get; set; }

        [MaxLength(1000)]
        public string? ExitNotes { get; set; }

        public Animal? Animal { get; set; }
    }
}
