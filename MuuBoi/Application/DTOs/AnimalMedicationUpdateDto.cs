using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Application.DTOs
{
    public class AnimalMedicationUpdateDto
    {
        public int? MedicationId { get; set; }

        [MaxLength(200, ErrorMessage = "O diagnóstico deve ter no máximo 200 caracteres.")]
        public string? Diagnosis { get; set; }

        public DateTime? ApplicationDate { get; set; }

        public DateTime? EndDate { get; set; }

        [MaxLength(200, ErrorMessage = "A descrição da dosagem deve ter no máximo 200 caracteres.")]
        public string? DosageDescription { get; set; }

        public int? WithdrawalPeriodDays { get; set; }

        [MaxLength(100, ErrorMessage = "O responsável deve ter no máximo 100 caracteres.")]
        public string? Responsible { get; set; }

        [MaxLength(500, ErrorMessage = "As observações devem ter no máximo 500 caracteres.")]
        public string? Observations { get; set; }
    }
}
