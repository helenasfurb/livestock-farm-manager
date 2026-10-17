namespace MuuBoi.Application.DTOs
{
    public class PortalSummaryDto
    {
        public int Days { get; set; }
        public PortalPropertyTotalsDto Properties { get; set; } = new();
        public PortalUserTotalsDto Users { get; set; } = new();
        public PortalAnimalTotalsDto Animals { get; set; } = new();
        public PortalUsageTotalsDto Usage { get; set; } = new();
        public List<PortalDailyUsageDto> DailyUsage { get; set; } = new();
        public List<PortalMonthlyCountDto> NewPropertiesByMonth { get; set; } = new();
    }

    public class PortalPropertyTotalsDto
    {
        public int Total { get; set; }
        public int New { get; set; }
        public int Active { get; set; }
    }

    public class PortalUserTotalsDto
    {
        public int Total { get; set; }
        public int Active { get; set; }
        public int Revoked { get; set; }
        public int Admins { get; set; }
        public int Members { get; set; }
        public int ActiveLastDay { get; set; }
        public int ActiveLastWeek { get; set; }
        public int ActiveLastMonth { get; set; }
    }

    public class PortalAnimalTotalsDto
    {
        public int Active { get; set; }
        public int Total { get; set; }
    }

    public class PortalUsageTotalsDto
    {
        public int Logins { get; set; }
        public int Pushes { get; set; }
        public int Pulls { get; set; }
    }

    public class PortalDailyUsageDto
    {
        public DateTime Date { get; set; }
        public int Logins { get; set; }
        public int Pushes { get; set; }
        public int Pulls { get; set; }
    }

    public class PortalMonthlyCountDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int Count { get; set; }
    }
}
