using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Application.DTOs
{
    public class PortalSummaryFilterDto
    {
        [Range(1, 365)]
        public int Days { get; set; } = 30;
    }

    public class PortalPropertyFilterDto
    {
        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        [Range(1, 100)]
        public int PageSize { get; set; } = 50;

        [RegularExpression("^(lastSeen|name|animals)$")]
        public string Sort { get; set; } = "lastSeen";

        [MaxLength(200)]
        public string? Search { get; set; }
    }
}
