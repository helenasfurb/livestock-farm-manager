using MuuBoi.Domain.Enums;

namespace MuuBoi.Application.DTOs
{
    public class MilkProductionFilterDto
    {
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public MilkingShift? Milking { get; set; }
        public bool? IsActive { get; set; }
    }
}
