using AutoMapper;
using MuuBoi.DTOs;
using MuuBoi.Models;

namespace MuuBoi.Mappings
{
    public class AnimalProfile : Profile
    {
        public AnimalProfile()
        {
            CreateMap<Animal, AnimalDto>()
                .ForMember(dest => dest.LastWeightRecord, opt => opt.MapFrom(src => src.WeightRecords != null ? src.WeightRecords.FirstOrDefault() : null));

            CreateMap<AnimalCreateDto, Animal>()
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow))
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(_ => true))
                .ForMember(dest => dest.UserId, opt => opt.Ignore())
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore());

            CreateMap<AnimalUpdateDto, Animal>()
                .ForAllMembers(opt => opt.Condition((_, _, srcMember) => srcMember != null));

            CreateMap<WeightRecord, WeightRecordDto>();
        }
    }
}
