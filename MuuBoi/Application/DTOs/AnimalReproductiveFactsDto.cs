namespace MuuBoi.Application.DTOs
{
    public class AnimalReproductiveFactsDto
    {
        public int AnimalId { get; set; }
        public string? Name { get; set; }
        public string? TagNumber { get; set; }
        public bool HasActiveConfirmedPregnancy { get; set; }
        public DateTime? LastCalvingDate { get; set; }
        public DateTime? LastAwaitingBreedingDate { get; set; }
    }
}
