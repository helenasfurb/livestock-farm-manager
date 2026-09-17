using AutoMapper;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Helpers;
using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Mappings
{
    public class MilkProductionProfile : Profile
    {
        public MilkProductionProfile()
        {
            CreateMap<MilkProduction, MilkProductionDto>()
                .ForMember(dest => dest.Milking,
                    opt => opt.MapFrom(src => src.Milking.HasValue
                        ? new EnumValueDto { Value = (int)src.Milking.Value, Label = src.Milking.Value.GetDescription() }
                        : null));

            CreateMap<MilkProduction, MilkProductionListItemDto>()
                .ForMember(dest => dest.Milking,
                    opt => opt.MapFrom(src => src.Milking.HasValue
                        ? new EnumValueDto { Value = (int)src.Milking.Value, Label = src.Milking.Value.GetDescription() }
                        : null));

            CreateMap<MilkProductionCreateDto, MilkProduction>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(_ => true))
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow))
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.PropertyId, opt => opt.Ignore());

            // PATCH (MilkProductionUpdateDto -> MilkProduction) é aplicado campo a campo
            // no MilkProductionService.UpdateAsync: mapear um DateTime? nulo sobre o
            // DateTime não-anulável da entidade gravaria 0001-01-01, então não usamos AutoMapper aqui.
        }
    }
}
