using MuuBoi.Domain.Enums;

namespace MuuBoi.Application.DTOs
{
    public class AnimalFilterDto
    {
        public string? TagNumber { get; set; }
        public string? Name { get; set; }
        public AnimalClassification? Classification { get; set; }
        public AnimalBreed? Breed { get; set; }
        public bool? IsActive { get; set; }
    }
}
