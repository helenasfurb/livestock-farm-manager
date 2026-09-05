using AutoMapper;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Helpers;
using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Mappings
{
    public class AnimalPregnancyProfile : Profile
    {
        public AnimalPregnancyProfile()
        {
            CreateMap<AnimalPregnancy, AnimalPregnancyDto>()
                .ForMember(dest => dest.AnimalTagNumber,
                    opt => opt.MapFrom(src => src.Animal != null ? src.Animal.TagNumber : string.Empty))
                .ForMember(dest => dest.SireAnimalTagNumber,
                    opt => opt.MapFrom(src => src.SireAnimal != null ? src.SireAnimal.TagNumber : null))
                .ForMember(dest => dest.SemenSampleName,
                    opt => opt.MapFrom(src => src.SemenSample != null ? src.SemenSample.Name : null))
                .ForMember(dest => dest.Status,
                    opt => opt.MapFrom(src => new EnumValueDto
                    {
                        Value = (int)src.Status,
                        Label = src.Status.GetDescription()
                    }))
                .ForMember(dest => dest.Calving,
                    opt => opt.MapFrom(src => src.Calvings != null
                        ? src.Calvings.FirstOrDefault(c => c.IsActive)
                        : null));

            CreateMap<AnimalPregnancy, AnimalPregnancyListItemDto>()
                .ForMember(dest => dest.AnimalTagNumber,
                    opt => opt.MapFrom(src => src.Animal != null ? src.Animal.TagNumber : string.Empty))
                .ForMember(dest => dest.AnimalName,
                    opt => opt.MapFrom(src => src.Animal != null ? src.Animal.Name : null))
                .ForMember(dest => dest.Status,
                    opt => opt.MapFrom(src => new EnumValueDto
                    {
                        Value = (int)src.Status,
                        Label = src.Status.GetDescription()
                    }));
        }
    }
}
