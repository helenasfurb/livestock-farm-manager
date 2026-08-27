using MuuBoi.Domain.Enums;

namespace MuuBoi.Application.DTOs
{
    public class AnimalPregnancyFilterDto
    {
        public int? AnimalId { get; set; }
        public AnimalPregnancyStatus? Status { get; set; }
        public bool? IsActive { get; set; }
    }
}
