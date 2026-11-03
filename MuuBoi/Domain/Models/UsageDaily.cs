namespace MuuBoi.Domain.Models
{
    public class UsageDaily : BaseEntity
    {
        public Guid PropertyId { get; set; }

        public DateTime Date { get; set; }

        public int Logins { get; set; }

        public int Pushes { get; set; }

        public int Pulls { get; set; }

        public Property? Property { get; set; }
    }
}
