using AutoMapper;
using MuuBoi.Application.DTOs;
using MuuBoi.DTOs;
using MuuBoi.Models;


namespace Application.Mappings
{
    public class WeightRecordProfile : Profile
    {
            public WeightRecordProfile()
            {
                CreateMap<WeightRecord, WeightRecordDto>();
                CreateMap<WeightRecordCreateDto, WeightRecord>()
                    .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow))
                    .ForMember(dest => dest.IsActive, opt => opt.MapFrom(_ => true))
                    .ForMember(dest => dest.Id, opt => opt.Ignore())
                    .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore());

                CreateMap<WeightRecordUpdateDto, WeightRecord>()
                    .ForMember(dest => dest.RecordedAt, opt => opt.MapFrom(src => src.WeightDate))
                    .ForMember(dest => dest.Observations, opt => opt.MapFrom(src => src.WeightObservations))
                    .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow))
                    .ForMember(dest => dest.Id, opt => opt.Ignore())
                    .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                    .ForMember(dest => dest.IsActive, opt => opt.Ignore())
                    .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

            }
    }
}
