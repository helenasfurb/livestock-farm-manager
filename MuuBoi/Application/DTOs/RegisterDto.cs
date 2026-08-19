using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Application.DTOs
{
    public class RegisterDto
    {
        [Required]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string PropertyName { get; set; } = string.Empty;
    }
}
