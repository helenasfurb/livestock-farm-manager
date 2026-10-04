using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Application.DTOs
{
    public class UpdateProfileDto
    {
        [MaxLength(150, ErrorMessage = "O nome deve ter no máximo 150 caracteres.")]
        [RegularExpression(@".*\S.*", ErrorMessage = "O nome não pode ficar em branco.")]
        public string? Name { get; set; }

        [MaxLength(20, ErrorMessage = "O telefone deve ter no máximo 20 caracteres.")]
        [RegularExpression(@"^$|^\+?[\d\s\-\(\)]{8,20}$", ErrorMessage = "Telefone inválido.")]
        public string? PhoneNumber { get; set; }
    }
}
