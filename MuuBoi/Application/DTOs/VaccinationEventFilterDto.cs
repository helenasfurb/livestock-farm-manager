using MuuBoi.Domain.Enums;

namespace MuuBoi.Application.DTOs
{
    public class VaccinationEventFilterDto
    {
        public int? VaccineId { get; set; }
        public int? AnimalId { get; set; }
        public VaccinationEventStatus? Status { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public bool? IsActive { get; set; }
    }
}
