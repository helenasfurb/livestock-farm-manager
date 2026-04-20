using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Models
{
    public class ApplicationUser : IdentityUser
    {
        [MaxLength(150)]
        public string FullName { get; set; } = string.Empty;
    }
}
