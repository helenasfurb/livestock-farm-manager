using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Application.DTOs
{
    public class ResendConfirmationDto
    {
        [Required(ErrorMessage = "O e-mail é obrigatório.")]
        [EmailAddress(ErrorMessage = "O e-mail informado é inválido.")]
        public string Email { get; set; } = string.Empty;
    }
}
