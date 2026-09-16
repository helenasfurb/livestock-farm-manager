using MuuBoi.Domain.Enums;

namespace MuuBoi.Application.DTOs
{
    /// <summary>
    /// Raw herd-composition fact per active animal, gathered set-based (no GROUP BY in SQL).
    /// The service projects the distributions in memory so the same derivation can run over the
    /// local cache when offline. Internal computation carrier, not a transport DTO.
    /// </summary>
    public class AnimalCompositionFact
    {
        public AnimalClassification? Classification { get; set; }
        public AnimalGender? Gender { get; set; }
        public AnimalBreed? Breed { get; set; }
    }

    /// <summary>
    /// Raw vaccination-event fact feeding the applied-per-month and overdue projections. Status is
    /// derived on read by <c>VaccinationEventStatusResolver</c>, never stored. Internal carrier.
    /// </summary>
    public class VaccinationEventFact
    {
        public int VaccinationEventId { get; set; }
        public string VaccineName { get; set; } = string.Empty;
        public DateTime? ApplicationDate { get; set; }
        public DateTime? PredictedDate { get; set; }
        public int AnimalCount { get; set; }
    }
}
