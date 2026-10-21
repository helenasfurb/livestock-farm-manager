using AutoMapper;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Helpers;
using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Mappings
{
    public class AnimalCalvingProfile : Profile
    {
        public AnimalCalvingProfile()
        {
            CreateMap<AnimalCalving, AnimalCalvingDto>()
                .ForMember(dest => dest.Calves,
                    opt => opt.MapFrom(src => src.Calves != null
                        ? src.Calves.Where(c => c.IsActive)
                        : Enumerable.Empty<AnimalCalvingCalf>()));

            CreateMap<AnimalCalving, AnimalCalvingSyncDto>()
                .ForMember(dest => dest.Calves, opt => opt.Ignore());

            CreateMap<AnimalCalvingCalf, AnimalCalvingCalfSyncDto>()
                .ForMember(dest => dest.Sex,
                    opt => opt.MapFrom(src => new EnumValueDto
                    {
                        Value = (int)src.Sex,
                        Label = src.Sex.GetDescription()
                    }))
                .ForMember(dest => dest.VitalStatus,
                    opt => opt.MapFrom(src => new EnumValueDto
                    {
                        Value = (int)src.VitalStatus,
                        Label = src.VitalStatus.GetDescription()
                    }));

            CreateMap<AnimalCalving, AnimalCalvingCreatedDto>()
                .IncludeBase<AnimalCalving, AnimalCalvingDto>()
                .ForMember(dest => dest.Lactation, opt => opt.Ignore())
                .ForMember(dest => dest.BodyCondition, opt => opt.Ignore());

            CreateMap<AnimalCalvingCalf, AnimalCalvingCalfDto>()
                .ForMember(dest => dest.AnimalSyncId,
                    opt => opt.MapFrom(src => src.Animal != null ? src.Animal.SyncId : (Guid?)null))
                .ForMember(dest => dest.BirthWeight,
                    opt => opt.MapFrom(src => src.Animal != null && src.Animal.WeightRecords != null && src.Calving != null
                        ? src.Animal.WeightRecords
                            .Where(w => w.IsActive && w.RecordedAt == src.Calving.CalvingDate)
                            .Select(w => new SyncRefDto { Id = w.Id, SyncId = w.SyncId })
                            .FirstOrDefault()
                        : null))
                .ForMember(dest => dest.Name,
                    opt => opt.MapFrom(src => src.Animal != null ? src.Animal.Name : null))
                .ForMember(dest => dest.TagNumber,
                    opt => opt.MapFrom(src => src.Animal != null ? src.Animal.TagNumber : null))
                .ForMember(dest => dest.PropertyTagNumber,
                    opt => opt.MapFrom(src => src.Animal != null ? src.Animal.PropertyTagNumber : null))
                .ForMember(dest => dest.Breed,
                    opt => opt.MapFrom(src => src.Animal != null && src.Animal.Breed.HasValue
                        ? new EnumValueDto
                        {
                            Value = (int)src.Animal.Breed.Value,
                            Label = src.Animal.Breed.Value.GetDescription()
                        }
                        : null))
                .ForMember(dest => dest.Sex,
                    opt => opt.MapFrom(src => new EnumValueDto
                    {
                        Value = (int)src.Sex,
                        Label = src.Sex.GetDescription()
                    }))
                .ForMember(dest => dest.VitalStatus,
                    opt => opt.MapFrom(src => new EnumValueDto
                    {
                        Value = (int)src.VitalStatus,
                        Label = src.VitalStatus.GetDescription()
                    }));

            CreateMap<AnimalCalvingCalfCreateDto, AnimalCalvingCalf>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.SyncId, opt => opt.Ignore())
                .ForMember(dest => dest.CalvingId, opt => opt.Ignore())
                .ForMember(dest => dest.AnimalId, opt => opt.Ignore())
                .ForMember(dest => dest.PropertyId, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(_ => true))
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow))
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.Calving, opt => opt.Ignore())
                .ForMember(dest => dest.Animal, opt => opt.Ignore());
        }
    }
}
