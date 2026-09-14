using MuuBoi.Application.DTOs;
using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Helpers
{
    public static class GenealogyResolver
    {
        public static AnimalParentageDto? Resolve(AnimalCalvingCalf? birth)
        {
            var calving = birth?.Calving;
            if (calving == null)
                return null;

            var mother = ResolveMother(calving.Animal);
            var father = ResolveFather(calving.AnimalPregnancy);

            if (mother == null && father == null)
                return null;

            return new AnimalParentageDto { Mother = mother, Father = father };
        }

        private static AnimalRefDto? ResolveMother(Animal? mother)
            => mother == null
                ? null
                : new AnimalRefDto { Id = mother.Id, Name = mother.Name, TagNumber = mother.TagNumber };

        private static GenealogyFatherDto? ResolveFather(AnimalPregnancy? pregnancy)
        {
            if (pregnancy == null)
                return null;

            var sire = pregnancy.SireAnimal ?? pregnancy.BreedingEvent?.SireAnimal;
            if (sire != null)
                return new GenealogyFatherDto
                {
                    Type = "Bull",
                    Bull = new AnimalRefDto { Id = sire.Id, Name = sire.Name, TagNumber = sire.TagNumber }
                };

            var semen = pregnancy.SemenSample ?? pregnancy.BreedingEvent?.SemenSample;
            if (semen != null)
                return new GenealogyFatherDto
                {
                    Type = "Semen",
                    Semen = new SemenSireRefDto
                    {
                        SemenSampleId = semen.Id,
                        Name = semen.Name,
                        BullRegistration = semen.BullRegistration,
                        BullBreed = semen.BullBreed.HasValue
                            ? new EnumValueDto { Value = (int)semen.BullBreed.Value, Label = semen.BullBreed.Value.GetDescription() }
                            : null,
                        GeneticsCompany = semen.GeneticsCompany
                    }
                };

            return null;
        }
    }
}
