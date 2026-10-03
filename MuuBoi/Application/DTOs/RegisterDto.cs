using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Application.DTOs
{
    public class RegisterDto
    {
        [Required(ErrorMessage = "O nome é obrigatório.")]
        [MaxLength(150, ErrorMessage = "O nome deve ter no máximo 150 caracteres.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "O e-mail é obrigatório.")]
        [EmailAddress(ErrorMessage = "O e-mail informado é inválido.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "A senha é obrigatória.")]
        [MinLength(6, ErrorMessage = "A senha deve ter no mínimo 6 caracteres.")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "O nome da propriedade é obrigatório.")]
        [MaxLength(200, ErrorMessage = "O nome da propriedade deve ter no máximo 200 caracteres.")]
        public string PropertyName { get; set; } = string.Empty;
    }
}
