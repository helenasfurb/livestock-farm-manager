using AutoMapper;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Helpers;
using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Mappings
{
    public class SemenSampleProfile : Profile
    {
        public SemenSampleProfile()
        {
            CreateMap<SemenSample, SemenSampleDto>()
                .ForMember(dest => dest.BullBreed,
                    opt => opt.MapFrom(src => src.BullBreed.HasValue
                        ? new EnumValueDto { Value = (int)src.BullBreed.Value, Label = src.BullBreed.Value.GetDescription() }
                        : null))
                .ForMember(dest => dest.AvailableDoses, opt => opt.Ignore());

            CreateMap<SemenSample, SemenSampleCreatedDto>()
                .IncludeBase<SemenSample, SemenSampleDto>()
                .ForMember(dest => dest.InitialMovement, opt => opt.Ignore());

            CreateMap<SemenSample, SemenSampleListItemDto>()
                .ForMember(dest => dest.BullBreed,
                    opt => opt.MapFrom(src => src.BullBreed.HasValue
                        ? new EnumValueDto { Value = (int)src.BullBreed.Value, Label = src.BullBreed.Value.GetDescription() }
                        : null))
                .ForMember(dest => dest.AvailableDoses, opt => opt.Ignore());

            CreateMap<SemenSample, SemenSampleAutocompleteItemDto>();

            CreateMap<SemenSampleCreateDto, SemenSample>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(_ => true))
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow))
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.PropertyId, opt => opt.Ignore())
                .ForMember(dest => dest.SyncId, opt => opt.Ignore())
                .ForMember(dest => dest.RowVersion, opt => opt.Ignore())
                .ForMember(dest => dest.Movements, opt => opt.Ignore());

            CreateMap<SemenSampleUpdateDto, SemenSample>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.PropertyId, opt => opt.Ignore())
                .ForMember(dest => dest.SyncId, opt => opt.Ignore())
                .ForMember(dest => dest.RowVersion, opt => opt.Ignore())
                .ForMember(dest => dest.Movements, opt => opt.Ignore())
                .ForAllMembers(opt => opt.Condition((src, dest, srcMember) => srcMember != null));
        }
    }
}
