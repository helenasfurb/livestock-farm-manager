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

            }
    }
}
