using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Application.DTOs
{
    public class DeleteAccountDto
    {
        [Required]
        public string Password { get; set; } = string.Empty;
    }
}
