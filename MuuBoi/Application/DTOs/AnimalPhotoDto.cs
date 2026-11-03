namespace MuuBoi.Application.DTOs
{
    public class AnimalPhotoDto
    {
        public string? PhotoHash { get; set; }
        public string? PhotoUrl { get; set; }
        public string? ThumbnailUrl { get; set; }
        public DateTime? PhotoUpdatedAt { get; set; }
    }
}
