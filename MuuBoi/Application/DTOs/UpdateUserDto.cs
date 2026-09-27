using MuuBoi.Application.Helpers;
using MuuBoi.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Application.DTOs
{
    public class UpdateUserDto
    {
        [MaxLength(150)]
        public string? Name { get; set; }

        [ValidEnum(typeof(UserRole))]
        public UserRole? Role { get; set; }
    }
}
