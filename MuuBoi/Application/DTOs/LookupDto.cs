using Microsoft.IdentityModel.Abstractions;
using System.Globalization;

namespace MuuBoi.Application.DTOs
{
    public class LookupDto
    {
        public int Value { get; set; }
        public string Label { get; set; } = string.Empty;
    }
}
