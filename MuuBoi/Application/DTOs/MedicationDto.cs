namespace MuuBoi.DTOs
{
    public class MedicationDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Manufacturer { get; set; }
        public string? ActiveIngredient { get; set; }
        public int? DefaultWithdrawalPeriodDays { get; set; }
    }
}
