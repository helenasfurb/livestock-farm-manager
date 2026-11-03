namespace MuuBoi.Application.DTOs
{
    public class PortalTotalsFact
    {
        public int Properties { get; set; }
        public int NewProperties { get; set; }
        public int ActiveProperties { get; set; }
        public int Users { get; set; }
        public int ActiveUsers { get; set; }
        public int Admins { get; set; }
        public int SeenLastDay { get; set; }
        public int SeenLastWeek { get; set; }
        public int SeenLastMonth { get; set; }
        public int ActiveAnimals { get; set; }
        public int TotalAnimals { get; set; }
    }

    public class PortalMonthFact
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int Count { get; set; }
    }
}
