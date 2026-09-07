using AutoMapper;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Helpers;
using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Mappings
{
    public class HealthCaseProfile : Profile
    {
        public HealthCaseProfile()
        {
            // Derived fields (Status, liberation dates, decomposed quarters, nested collections)
            // are set in the service after mapping — same pattern as VaccinationEventProfile.
            CreateMap<HealthCase, HealthCaseListItemDto>()
                .ForMember(d => d.AnimalName, o => o.MapFrom(s => s.Animal != null ? s.Animal.Name : null))
                .ForMember(d => d.AnimalTagNumber, o => o.MapFrom(s => s.Animal != null ? s.Animal.TagNumber : null))
                .ForMember(d => d.DiseaseType, o => o.MapFrom(s => new EnumValueDto { Value = (int)s.DiseaseType, Label = s.DiseaseType.GetDescription() }))
                .ForMember(d => d.Status, o => o.Ignore())
                .ForMember(d => d.MilkLiberationDate, o => o.Ignore())
                .ForMember(d => d.AffectedQuarters, o => o.Ignore());

            CreateMap<HealthCase, HealthCaseDto>()
                .ForMember(d => d.AnimalName, o => o.MapFrom(s => s.Animal != null ? s.Animal.Name : null))
                .ForMember(d => d.AnimalTagNumber, o => o.MapFrom(s => s.Animal != null ? s.Animal.TagNumber : null))
                .ForMember(d => d.DiseaseType, o => o.MapFrom(s => new EnumValueDto { Value = (int)s.DiseaseType, Label = s.DiseaseType.GetDescription() }))
                .ForMember(d => d.Status, o => o.Ignore())
                .ForMember(d => d.CaseLiberationDate, o => o.Ignore())
                .ForMember(d => d.AffectedQuarters, o => o.Ignore())
                .ForMember(d => d.Medications, o => o.Ignore())
                .ForMember(d => d.Tests, o => o.Ignore());

            CreateMap<AnimalMedication, MedicationUseDto>()
                .ForMember(d => d.Dose, o => o.MapFrom(s => s.DosageDescription))
                .ForMember(d => d.MilkLiberationDate, o => o.Ignore());

            CreateMap<MastitisTest, MastitisTestDto>()
                .ForMember(d => d.TestType, o => o.MapFrom(s => new EnumValueDto { Value = (int)s.TestType, Label = s.TestType.GetDescription() }));
        }
    }
}
