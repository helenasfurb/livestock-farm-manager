using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Application.DTOs
{
    public class DeleteAccountDto
    {
        [Required(ErrorMessage = "A senha é obrigatória.")]
        public string Password { get; set; } = string.Empty;
    }
}
