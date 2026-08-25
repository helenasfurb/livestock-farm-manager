namespace MuuBoi.Application.DTOs
{
    public class SemenSampleAutocompleteItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? BatchNumber { get; set; }
        public DateTime? BatchDate { get; set; }
    }
}
