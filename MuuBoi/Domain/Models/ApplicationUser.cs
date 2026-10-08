using Microsoft.AspNetCore.Identity;
using MuuBoi.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Domain.Models
{
    public class ApplicationUser : IdentityUser
    {
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        public Guid PropertyId { get; set; }

        public bool IsActive { get; set; } = true;

        public UserRole Role { get; set; } = UserRole.Member;

        public Property? Property { get; set; }
    }
}
