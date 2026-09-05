using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Application.DTOs
{
    /// <summary>
    /// Input to create a booster dose from a parent event. Vaccine, animals and DoseType are
    /// inherited/assigned by the service; the only input is the predicted date of the new dose.
    /// </summary>
    public class VaccinationBoosterCreateDto
    {
        [Required(ErrorMessage = "A data prevista é obrigatória.")]
        public DateTime PredictedDate { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }
    }
}
