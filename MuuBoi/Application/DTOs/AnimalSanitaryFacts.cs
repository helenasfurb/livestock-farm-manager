namespace MuuBoi.Application.DTOs
{
    /// <summary>
    /// Per-animal facts feeding <c>AnimalSanitaryStatusResolver</c>, gathered set-based (no N+1).
    /// Not a transport DTO — an internal computation carrier.
    /// </summary>
    public class AnimalSanitaryFacts
    {
        public bool HasCaseUnderTreatment { get; set; }
        public DateTime? MilkWithheldUntil { get; set; }
        public bool HasSuspectedCase { get; set; }
    }
}
