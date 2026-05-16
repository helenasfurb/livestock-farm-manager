using AutoMapper;
using MuuBoi.Application.DTOs;
using MuuBoi.DTOs;
using MuuBoi.Models;

namespace Application.Mappings
{
    public class AnimalMedicationProfile : Profile
    {
        public AnimalMedicationProfile()
        {
            CreateMap<AnimalMedication, AnimalMedicationDto>()
                .ForMember(dest => dest.MedicationName, opt => opt.MapFrom(src => src.Medication != null ? src.Medication.Name : string.Empty));

            CreateMap<AnimalMedicationUpdateDto, AnimalMedication>()
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow))
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.AnimalId, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.Ignore())
                .ForMember(dest => dest.Animal, opt => opt.Ignore())
                .ForMember(dest => dest.Medication, opt => opt.Ignore())
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));
        }
    }
}
