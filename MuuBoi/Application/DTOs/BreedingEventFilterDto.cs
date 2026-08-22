using MuuBoi.Domain.Enums;

namespace MuuBoi.Application.DTOs
{
    public class BreedingEventFilterDto
    {
        public int? AnimalId { get; set; }
        public ReproductionType? ReproductionType { get; set; }
        public ReproductiveEventStatus? Status { get; set; }
        public DateTime? BreedingDateFrom { get; set; }
        public DateTime? BreedingDateTo { get; set; }
        public bool? IsActive { get; set; }
    }
}
