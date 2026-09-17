using MuuBoi.Domain.Enums;

namespace MuuBoi.Application.DTOs
{
    public class HealthCaseFilterDto
    {
        public DiseaseType? DiseaseType { get; set; }
        public int? AnimalId { get; set; }
        public HealthCaseStatus? Status { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public bool? IsActive { get; set; }
    }
}
