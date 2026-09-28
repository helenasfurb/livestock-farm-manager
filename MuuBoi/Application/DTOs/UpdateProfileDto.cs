using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Application.DTOs
{
    public class UpdateProfileDto
    {
        [MaxLength(150)]
        [RegularExpression(@".*\S.*", ErrorMessage = "O nome não pode ficar em branco.")]
        public string? Name { get; set; }

        [MaxLength(20)]
        [RegularExpression(@"^$|^\+?[\d\s\-\(\)]{8,20}$", ErrorMessage = "Telefone inválido.")]
        public string? PhoneNumber { get; set; }
    }
}
