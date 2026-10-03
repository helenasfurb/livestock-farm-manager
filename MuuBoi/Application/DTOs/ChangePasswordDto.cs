using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Application.DTOs
{
    public class ChangePasswordDto
    {
        [Required(ErrorMessage = "A senha atual é obrigatória.")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "A nova senha é obrigatória.")]
        [MinLength(6, ErrorMessage = "A nova senha deve ter no mínimo 6 caracteres.")]
        public string NewPassword { get; set; } = string.Empty;
    }
}
