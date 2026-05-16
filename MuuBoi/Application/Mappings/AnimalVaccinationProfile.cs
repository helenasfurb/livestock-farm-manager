using AutoMapper;
using MuuBoi.Application.DTOs;
using MuuBoi.DTOs;
using MuuBoi.Models;

namespace Application.Mappings
{
    public class AnimalVaccinationProfile : Profile
    {
        public AnimalVaccinationProfile()
        {
            CreateMap<AnimalVaccination, AnimalVaccinationDto>()
                .ForMember(dest => dest.VaccineName, opt => opt.MapFrom(src => src.Vaccine != null ? src.Vaccine.Name : string.Empty));

            CreateMap<AnimalVaccinationUpdateDto, AnimalVaccination>()
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow))
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.AnimalId, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.Ignore())
                .ForMember(dest => dest.Animal, opt => opt.Ignore())
                .ForMember(dest => dest.Vaccine, opt => opt.Ignore())
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));
        }
    }
}
