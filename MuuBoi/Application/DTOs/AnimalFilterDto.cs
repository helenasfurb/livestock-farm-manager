using System.ComponentModel.DataAnnotations;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Application.DTOs
{
    public class AnimalFilterDto : IValidatableObject
    {
        public string? TagNumber { get; set; }
        public string? Name { get; set; }
        public AnimalClassification? Classification { get; set; }
        public AnimalBreed? Breed { get; set; }
        public bool? WithoutBreed { get; set; }
        public ReproductiveStatus? ReproductiveStatus { get; set; }
        public SanitaryStatus? SanitaryStatus { get; set; }
        public bool? MilkWithheldOnly { get; set; }   // true = só animais com leite retido agora (carência futura)
        public bool? IsActive { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (WithoutBreed == true && Breed.HasValue)
                yield return new ValidationResult(
                    "Informe a raça ou 'sem raça registrada', não os dois.",
                    new[] { nameof(Breed), nameof(WithoutBreed) });
        }
    }
}
