using MuuBoi.Converters;
using System.Text.Json.Serialization;

namespace MuuBoi.DTOs
{
    public class AnimalDto
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Gender { get; set; }

        [JsonConverter(typeof(NullableDateFormatConverter))]
        public DateTime? BirthDate { get; set; }

        public string? TagNumber { get; set; }

        public bool IsActive { get; set; }

        public BreedResponseDto? Breed { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public IEnumerable<WeightRecordDto>? WeightRecords { get; set; }
    }
}
