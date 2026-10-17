namespace MuuBoi.Domain.Models
{
    public class LoginEvent : BaseEntity
    {
        public string UserId { get; set; } = string.Empty;

        public Guid PropertyId { get; set; }

        public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

        public ApplicationUser? User { get; set; }

        public Property? Property { get; set; }
    }
}
