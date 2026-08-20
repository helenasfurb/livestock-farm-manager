using AutoMapper;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Helpers;
using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Mappings
{
    public class AnimalProfile : Profile
    {
        public AnimalProfile()
        {
            CreateMap<Animal, AnimalDto>()
                .ForMember(dest => dest.Gender,
                    opt => opt.MapFrom(src => src.Gender.HasValue
                        ? new EnumValueDto { Value = (int)src.Gender.Value, Label = src.Gender.Value.GetDescription() }
                        : null))
                .ForMember(dest => dest.Breed,
                    opt => opt.MapFrom(src => src.Breed.HasValue
                        ? new EnumValueDto { Value = (int)src.Breed.Value, Label = src.Breed.Value.GetDescription() }
                        : null))
                .ForMember(dest => dest.Classification,
                    opt => opt.MapFrom(src => src.Classification.HasValue
                        ? new EnumValueDto { Value = (int)src.Classification.Value, Label = src.Classification.Value.GetDescription() }
                        : null))
                .ForMember(dest => dest.Purpose,
                    opt => opt.MapFrom(src => src.Purpose.HasValue
                        ? new EnumValueDto { Value = (int)src.Purpose.Value, Label = src.Purpose.Value.GetDescription() }
                        : null))
                .ForMember(dest => dest.Origin,
                    opt => opt.MapFrom(src => src.Origin.HasValue
                        ? new EnumValueDto { Value = (int)src.Origin.Value, Label = src.Origin.Value.GetDescription() }
                        : null))
                .ForMember(dest => dest.ExitReason,
                    opt => opt.MapFrom(src => src.ExitReason.HasValue
                        ? new EnumValueDto { Value = (int)src.ExitReason.Value, Label = src.ExitReason.Value.GetDescription() }
                        : null))
                .ForMember(dest => dest.DeathCause,
                    opt => opt.MapFrom(src => src.DeathCause.HasValue
                        ? new EnumValueDto { Value = (int)src.DeathCause.Value, Label = src.DeathCause.Value.GetDescription() }
                        : null))
                .ForMember(dest => dest.LastWeightRecord,
                    opt => opt.MapFrom(src => src.WeightRecords != null ? src.WeightRecords.FirstOrDefault() : null))
                .ForMember(dest => dest.WeightRecords,
                    opt => opt.MapFrom(src => src.WeightRecords));

            CreateMap<Animal, AnimalListItemDto>()
                .ForMember(dest => dest.Classification,
                    opt => opt.MapFrom(src => src.Classification.HasValue
                        ? new EnumValueDto { Value = (int)src.Classification.Value, Label = src.Classification.Value.GetDescription() }
                        : null))
                .ForMember(dest => dest.Breed,
                    opt => opt.MapFrom(src => src.Breed.HasValue
                        ? new EnumValueDto { Value = (int)src.Breed.Value, Label = src.Breed.Value.GetDescription() }
                        : null))
                .ForMember(dest => dest.ExitReason,
                    opt => opt.MapFrom(src => src.ExitReason.HasValue
                        ? new EnumValueDto { Value = (int)src.ExitReason.Value, Label = src.ExitReason.Value.GetDescription() }
                        : null))
                .ForMember(dest => dest.LastWeightRecord,
                    opt => opt.MapFrom(src => src.WeightRecords != null ? src.WeightRecords.FirstOrDefault() : null));

            CreateMap<AnimalCreateDto, Animal>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.PropertyId, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow))
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(_ => true))
                .ForMember(dest => dest.WeightRecords, opt => opt.Ignore())
                .ForMember(dest => dest.AnimalVaccinations, opt => opt.Ignore())
                .ForMember(dest => dest.AnimalMedications, opt => opt.Ignore());

            CreateMap<AnimalUpdateDto, Animal>()
                .ForAllMembers(opt => opt.Condition((_, _, srcMember) => srcMember != null));

            CreateMap<WeightRecord, WeightRecordDto>();
        }
    }
}
