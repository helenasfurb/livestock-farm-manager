using System.ComponentModel.DataAnnotations;

namespace MuuBoi.DTOs
{
    public class CreateUserDto
    {
        [Required]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string TemporaryPassword { get; set; } = string.Empty;
    }

    public class UserResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}
