using MuuBoi.Application.Helpers;
using MuuBoi.Domain.Enums;
using System.Text.Json.Serialization;

namespace MuuBoi.Application.DTOs
{
    public class AnimalDto
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public AnimalGender? Gender { get; set; }

        [JsonConverter(typeof(NullableDateFormatConverter))]
        public DateTime? BirthDate { get; set; }

        public string? TagNumber { get; set; }

        public bool IsActive { get; set; }

        public BreedResponseDto? Breed { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public bool IsPregnant { get; set; }

        [JsonConverter(typeof(NullableDateFormatConverter))]
        public DateTime? ExpectedBirthDate { get; set; }

        public WeightRecordDto? LastWeightRecord{ get; set; }
    }
}
