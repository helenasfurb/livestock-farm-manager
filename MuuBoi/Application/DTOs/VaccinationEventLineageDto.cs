namespace MuuBoi.Application.DTOs
{
    /// <summary>
    /// A related event in the lineage (parent or child), summarized for the event detail:
    /// its dates and derived status, plus the id needed to edit it directly.
    /// </summary>
    public class VaccinationEventLineageDto
    {
        public int Id { get; set; }
        public EnumValueDto? DoseType { get; set; }
        public DateTime? PredictedDate { get; set; }
        public DateTime? ApplicationDate { get; set; }
        public EnumValueDto? Status { get; set; }
    }
}
