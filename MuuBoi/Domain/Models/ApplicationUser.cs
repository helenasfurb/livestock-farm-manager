using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Models
{
    public class ApplicationUser : IdentityUser
    {
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        public Guid PropertyId { get; set; }

        public bool IsActive { get; set; } = true;

        public Property? Property { get; set; }
    }
}
