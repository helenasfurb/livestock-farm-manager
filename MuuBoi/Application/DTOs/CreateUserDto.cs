using MuuBoi.Application.Helpers;
using MuuBoi.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Application.DTOs
{
    public class CreateUserDto
    {
        [Required(ErrorMessage = "O nome é obrigatório.")]
        [MaxLength(150, ErrorMessage = "O nome deve ter no máximo 150 caracteres.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "O e-mail é obrigatório.")]
        [EmailAddress(ErrorMessage = "O e-mail informado é inválido.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "A senha temporária é obrigatória.")]
        [MinLength(6, ErrorMessage = "A senha temporária deve ter no mínimo 6 caracteres.")]
        public string TemporaryPassword { get; set; } = string.Empty;

        [ValidEnum(typeof(UserRole))]
        public UserRole? Role { get; set; }
    }

    public class UserResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public EnumValueDto Role { get; set; } = new();
        public bool IsActive { get; set; }
    }
}
