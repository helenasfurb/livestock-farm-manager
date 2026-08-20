using System.ComponentModel.DataAnnotations;

namespace MuuBoi.Application.Helpers
{
    [AttributeUsage(AttributeTargets.Property)]
    public class ValidEnumAttribute : ValidationAttribute
    {
        private readonly Type _enumType;

        public ValidEnumAttribute(Type enumType)
        {
            _enumType = enumType;
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value == null) return ValidationResult.Success;

            if (!Enum.IsDefined(_enumType, value))
            {
                var valid = Enum.GetValues(_enumType)
                    .Cast<Enum>()
                    .Select(e => $"{e.GetDescription()} ({Convert.ToInt32(e)})")
                    .ToList();

                return new ValidationResult(
                    $"Valor inválido. Valores aceitos: {string.Join(", ", valid)}.",
                    new[] { validationContext.MemberName! });
            }

            return ValidationResult.Success;
        }
    }
}
