using AutoMapper;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Helpers;
using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Mappings
{
    public class BreedingEventProfile : Profile
    {
        public BreedingEventProfile()
        {
            CreateMap<BreedingEvent, BreedingEventDto>()
                .ForMember(dest => dest.AnimalTagNumber,
                    opt => opt.MapFrom(src => src.Animal != null ? src.Animal.TagNumber : string.Empty))
                .ForMember(dest => dest.ReproductionType,
                    opt => opt.MapFrom(src => new EnumValueDto
                    {
                        Value = (int)src.ReproductionType,
                        Label = src.ReproductionType.GetDescription()
                    }))
                .ForMember(dest => dest.Status,
                    opt => opt.MapFrom(src => new EnumValueDto
                    {
                        Value = (int)src.Status,
                        Label = src.Status.GetDescription()
                    }))
                .ForMember(dest => dest.SemenSampleName,
                    opt => opt.MapFrom(src => src.SemenSample != null ? src.SemenSample.Name : null))
                .ForMember(dest => dest.SireAnimalTagNumber,
                    opt => opt.MapFrom(src => src.SireAnimal != null ? src.SireAnimal.TagNumber : null))
                .ForMember(dest => dest.SireAnimalName,
                    opt => opt.MapFrom(src => src.SireAnimal != null ? src.SireAnimal.Name : null));

            CreateMap<BreedingEvent, BreedingEventListItemDto>()
                .ForMember(dest => dest.AnimalTagNumber,
                    opt => opt.MapFrom(src => src.Animal != null ? src.Animal.TagNumber : string.Empty))
                .ForMember(dest => dest.AnimalName,
                    opt => opt.MapFrom(src => src.Animal != null ? src.Animal.Name : null))
                .ForMember(dest => dest.ReproductionType,
                    opt => opt.MapFrom(src => new EnumValueDto
                    {
                        Value = (int)src.ReproductionType,
                        Label = src.ReproductionType.GetDescription()
                    }))
                .ForMember(dest => dest.Status,
                    opt => opt.MapFrom(src => new EnumValueDto
                    {
                        Value = (int)src.Status,
                        Label = src.Status.GetDescription()
                    }));

            CreateMap<BreedingEventCreateDto, BreedingEvent>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.AnimalId, opt => opt.Ignore())
                .ForMember(dest => dest.ServiceNumber, opt => opt.Ignore())
                .ForMember(dest => dest.Status, opt => opt.Ignore())
                .ForMember(dest => dest.DiagnosisDate, opt => opt.Ignore())
                .ForMember(dest => dest.PropertyId, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(_ => true))
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow))
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.Animal, opt => opt.Ignore())
                .ForMember(dest => dest.SemenSample, opt => opt.Ignore())
                .ForMember(dest => dest.SireAnimal, opt => opt.Ignore());
        }
    }
}
