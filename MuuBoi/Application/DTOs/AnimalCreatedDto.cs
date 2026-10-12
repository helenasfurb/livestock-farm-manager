namespace MuuBoi.Application.DTOs
{
    public class AnimalCreatedDto : AnimalDto
    {
        public LactationRefDto? InitialLactation { get; set; }
    }
}
