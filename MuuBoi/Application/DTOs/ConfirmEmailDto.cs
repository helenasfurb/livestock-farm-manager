using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Application.DTOs
{
    public class ConfirmEmailDto
    {
        [Required(ErrorMessage = "O e-mail é obrigatório.")]
        [EmailAddress(ErrorMessage = "O e-mail informado é inválido.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "O código é obrigatório.")]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "O código deve ter 6 dígitos.")]
        public string Code { get; set; } = string.Empty;
    }
}
