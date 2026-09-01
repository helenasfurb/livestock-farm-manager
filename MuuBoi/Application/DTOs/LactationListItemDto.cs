namespace MuuBoi.Application.DTOs
{
    public class LactationListItemDto
    {
        public int Id { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public EnumValueDto? Origin { get; set; }
        public bool IsLactating { get; set; }
        public int DaysInMilk { get; set; }
    }
}
