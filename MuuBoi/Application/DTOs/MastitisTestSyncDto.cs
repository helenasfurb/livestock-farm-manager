namespace MuuBoi.Application.DTOs
{
    public class MastitisTestSyncDto
    {
        public int Id { get; set; }
        public Guid SyncId { get; set; }
        public EnumValueDto? TestType { get; set; }
        public string Result { get; set; } = string.Empty;
        public DateTime TestDate { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
