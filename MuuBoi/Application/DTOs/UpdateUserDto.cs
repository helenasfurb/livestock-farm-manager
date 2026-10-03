using MuuBoi.Application.Helpers;
using MuuBoi.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Application.DTOs
{
    public class UpdateUserDto
    {
        [MaxLength(150, ErrorMessage = "O nome deve ter no máximo 150 caracteres.")]
        public string? Name { get; set; }

        [ValidEnum(typeof(UserRole))]
        public UserRole? Role { get; set; }
    }
}
