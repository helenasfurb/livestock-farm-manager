namespace MuuBoi.Domain.Models
{
    /// <summary>
    /// Link between a <see cref="VaccinationEvent"/> and an <see cref="Animal"/> — the
    /// "vaccine map per animal" (D2), read from the animal end. Composite key
    /// (VaccinationEventId, AnimalId). Extensible (Q1): may gain its own ApplicationDate/DoseType
    /// later, for per-animal effectuation, without breaking the event.
    /// </summary>
    public class VaccinationEventAnimal : ITenantEntity
    {
        public int VaccinationEventId { get; set; }
        public int AnimalId { get; set; }

        public Guid PropertyId { get; set; }

        public VaccinationEvent? VaccinationEvent { get; set; }
        public Animal? Animal { get; set; }
    }
}
