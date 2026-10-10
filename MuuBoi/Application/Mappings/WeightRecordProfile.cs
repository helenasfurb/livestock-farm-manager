using AutoMapper;
using MuuBoi.Application.DTOs;
using MuuBoi.Domain.Models;


namespace MuuBoi.Application.Mappings
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
                    .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                    .ForMember(dest => dest.SyncId, opt => opt.Ignore())
                    .ForMember(dest => dest.RowVersion, opt => opt.Ignore())
                    .ForMember(dest => dest.PropertyId, opt => opt.Ignore());

                CreateMap<WeightRecordUpdateDto, WeightRecord>()
                    .ForMember(dest => dest.Weight, opt => opt.PreCondition(src => src.Weight.HasValue))
                    .ForMember(dest => dest.RecordedAt, opt =>
                    {
                        opt.PreCondition(src => src.WeightDate.HasValue);
                        opt.MapFrom(src => src.WeightDate);
                    })
                    .ForMember(dest => dest.Observations, opt => opt.MapFrom(src => src.WeightObservations))
                    .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                    .ForMember(dest => dest.Id, opt => opt.Ignore())
                    .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                    .ForMember(dest => dest.IsActive, opt => opt.Ignore())
                    .ForMember(dest => dest.SyncId, opt => opt.Ignore())
                    .ForMember(dest => dest.RowVersion, opt => opt.Ignore())
                    .ForMember(dest => dest.AnimalId, opt => opt.Ignore())
                    .ForMember(dest => dest.PropertyId, opt => opt.Ignore())
                    .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

            }
    }
}
