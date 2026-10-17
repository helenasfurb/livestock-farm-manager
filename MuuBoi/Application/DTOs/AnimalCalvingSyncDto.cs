namespace MuuBoi.Application.DTOs
{
    public class AnimalCalvingSyncDto
    {
        public int Id { get; set; }
        public Guid SyncId { get; set; }
        public int AnimalPregnancyId { get; set; }
        public int AnimalId { get; set; }
        public DateTime CalvingDate { get; set; }
        public string? Notes { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<AnimalCalvingCalfSyncDto> Calves { get; set; } = new();
    }
}
