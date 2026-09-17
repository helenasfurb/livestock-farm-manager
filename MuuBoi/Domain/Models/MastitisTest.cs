using System.ComponentModel.DataAnnotations;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Domain.Models
{
    /// <summary>
    /// A mastitis test recorded at the case level (1:N with <see cref="HealthCase"/>).
    /// <see cref="Result"/> is free text to accommodate different natures (Alterado, ++, 350 mil cél/mL, S. aureus).
    /// </summary>
    public class MastitisTest : BaseEntity, ITenantEntity
    {
        [Required]
        public int HealthCaseId { get; set; }

        public MastitisTestType TestType { get; set; }

        [Required, MaxLength(200)]
        public string Result { get; set; } = string.Empty;

        public DateTime TestDate { get; set; }

        public Guid PropertyId { get; set; }

        public HealthCase? HealthCase { get; set; }
    }
}
