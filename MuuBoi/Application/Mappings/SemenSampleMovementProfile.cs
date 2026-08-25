using AutoMapper;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Helpers;
using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Mappings
{
    public class SemenSampleMovementProfile : Profile
    {
        public SemenSampleMovementProfile()
        {
            CreateMap<SemenSampleMovement, SemenSampleMovementDto>()
                .ForMember(dest => dest.SemenSampleName,
                    opt => opt.MapFrom(src => src.SemenSample != null ? src.SemenSample.Name : string.Empty))
                .ForMember(dest => dest.MovementType,
                    opt => opt.MapFrom(src => new EnumValueDto
                    {
                        Value = (int)src.MovementType,
                        Label = src.MovementType.GetDescription()
                    }));

            CreateMap<SemenSampleMovement, SemenSampleMovementListItemDto>()
                .ForMember(dest => dest.MovementType,
                    opt => opt.MapFrom(src => new EnumValueDto
                    {
                        Value = (int)src.MovementType,
                        Label = src.MovementType.GetDescription()
                    }));

            CreateMap<SemenSampleMovementCreateDto, SemenSampleMovement>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(_ => true))
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow))
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.PropertyId, opt => opt.Ignore())
                .ForMember(dest => dest.SemenSampleId, opt => opt.Ignore())
                .ForMember(dest => dest.BreedingEventId, opt => opt.Ignore())
                .ForMember(dest => dest.SemenSample, opt => opt.Ignore())
                .ForMember(dest => dest.BreedingEvent, opt => opt.Ignore());
        }
    }
}
