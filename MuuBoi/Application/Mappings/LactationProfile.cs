using AutoMapper;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Helpers;
using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Mappings
{
    public class LactationProfile : Profile
    {
        public LactationProfile()
        {
            CreateMap<Lactation, LactationDto>()
                .ForMember(dest => dest.AnimalTagNumber,
                    opt => opt.MapFrom(src => src.Animal != null ? src.Animal.TagNumber : null))
                .ForMember(dest => dest.Origin,
                    opt => opt.MapFrom(src => new EnumValueDto
                    {
                        Value = (int)src.Origin,
                        Label = src.Origin.GetDescription()
                    }))
                .ForMember(dest => dest.IsLactating, opt => opt.Ignore())
                .ForMember(dest => dest.DaysInMilk, opt => opt.Ignore());

            CreateMap<Lactation, LactationListItemDto>()
                .ForMember(dest => dest.Origin,
                    opt => opt.MapFrom(src => new EnumValueDto
                    {
                        Value = (int)src.Origin,
                        Label = src.Origin.GetDescription()
                    }))
                .ForMember(dest => dest.IsLactating, opt => opt.Ignore())
                .ForMember(dest => dest.DaysInMilk, opt => opt.Ignore());
        }
    }
}
