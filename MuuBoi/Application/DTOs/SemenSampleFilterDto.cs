using MuuBoi.Domain.Enums;

namespace MuuBoi.Application.DTOs
{
    public class SemenSampleFilterDto
    {
        public string? Name { get; set; }
        public AnimalBreed? BullBreed { get; set; }
        public bool? IsActive { get; set; }
    }
}
