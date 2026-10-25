using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace MuuBoi.Application.Helpers
{
    [AttributeUsage(AttributeTargets.Property)]
    public class ImageFileAttribute : ValidationAttribute
    {
        private readonly long _maxBytes;

        public ImageFileAttribute(long maxBytes)
        {
            _maxBytes = maxBytes;
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is not IFormFile file) return ValidationResult.Success;

            if (file.Length == 0 || file.Length > _maxBytes)
                return new ValidationResult(
                    $"O arquivo deve ter no máximo {_maxBytes / 1024} KB.",
                    new[] { validationContext.MemberName! });

            var detected = DetectContentType(file);
            if (detected == null || !string.Equals(detected, file.ContentType, StringComparison.OrdinalIgnoreCase))
                return new ValidationResult(
                    "Envie uma imagem JPEG ou WebP.",
                    new[] { validationContext.MemberName! });

            return ValidationResult.Success;
        }

        private static string? DetectContentType(IFormFile file)
        {
            var header = new byte[12];
            using var stream = file.OpenReadStream();
            var read = stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);

            if (read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
                return "image/jpeg";

            if (read >= 12
                && Encoding.ASCII.GetString(header, 0, 4) == "RIFF"
                && Encoding.ASCII.GetString(header, 8, 4) == "WEBP")
                return "image/webp";

            return null;
        }
    }
}
