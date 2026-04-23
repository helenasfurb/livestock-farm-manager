namespace MuuBoi.DTOs
{
    public class BreedDto
    {
            public int Id { get; set; }
            public string Name { get; set; } = null!;
            public string Description { get; set; } = null!;

            public DateTime CreatedAt { get; set; }

            public DateTime? UpdatedAt { get; set; }


    }
}
