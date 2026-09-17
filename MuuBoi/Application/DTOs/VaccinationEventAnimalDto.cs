namespace MuuBoi.Application.DTOs
{
    /// <summary>An animal included in a vaccination event (summary for the event detail).</summary>
    public class VaccinationEventAnimalDto
    {
        public int AnimalId { get; set; }
        public string? Name { get; set; }
        public string? TagNumber { get; set; }
    }
}
