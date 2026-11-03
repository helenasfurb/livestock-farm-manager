using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using MuuBoi.Application.Helpers;

namespace MuuBoi.Application.DTOs
{
    public class AnimalPhotoUploadDto
    {
        [Required(ErrorMessage = "A foto é obrigatória.")]
        [ImageFile(2 * 1024 * 1024)]
        public IFormFile? Photo { get; set; }

        [Required(ErrorMessage = "A miniatura é obrigatória.")]
        [ImageFile(200 * 1024)]
        public IFormFile? Thumbnail { get; set; }

        [Required(ErrorMessage = "O hash da foto é obrigatório.")]
        [RegularExpression("^[0-9a-fA-F]{64}$", ErrorMessage = "O hash da foto deve ser um SHA-256 em hexadecimal (64 caracteres).")]
        public string? PhotoHash { get; set; }

        [Required(ErrorMessage = "O hash da miniatura é obrigatório.")]
        [RegularExpression("^[0-9a-fA-F]{64}$", ErrorMessage = "O hash da miniatura deve ser um SHA-256 em hexadecimal (64 caracteres).")]
        public string? ThumbnailHash { get; set; }

        public DateTime? TakenAt { get; set; }
    }
}
