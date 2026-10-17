namespace MuuBoi.Application.DTOs
{
    public class PortalPropertyPageDto
    {
        public List<PortalPropertyRowDto> Items { get; set; } = new();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
    }

    public class PortalPropertyRowDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public int ActiveUsers { get; set; }
        public int TotalUsers { get; set; }
        public int ActiveAnimals { get; set; }
        public DateTime? LastSeenAt { get; set; }
        public int Logins { get; set; }
        public int Pushes { get; set; }
        public int Pulls { get; set; }
    }
}
