namespace MuuBoi.Application.DTOs
{
    public class AnimalCalvingCreatedDto : AnimalCalvingDto
    {
        public LactationRefDto? Lactation { get; set; }
        public SyncRefDto? BodyCondition { get; set; }
    }
}
