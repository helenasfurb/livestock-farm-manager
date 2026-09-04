using AutoMapper;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Helpers;
using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Mappings
{
    public class VaccinationEventProfile : Profile
    {
        public VaccinationEventProfile()
        {
            CreateMap<VaccinationEvent, VaccinationEventDto>()
                .ForMember(dest => dest.VaccineName,
                    opt => opt.MapFrom(src => src.Vaccine != null ? src.Vaccine.Name : null))
                .ForMember(dest => dest.DoseType,
                    opt => opt.MapFrom(src => new EnumValueDto
                    {
                        Value = (int)src.DoseType,
                        Label = src.DoseType.GetDescription()
                    }))
                .ForMember(dest => dest.Animals,
                    opt => opt.MapFrom(src => src.EventAnimals))
                // Status and lineage are derived (need "now" + a child lookup); set in the service.
                .ForMember(dest => dest.Status, opt => opt.Ignore())
                .ForMember(dest => dest.ParentEvent, opt => opt.Ignore())
                .ForMember(dest => dest.ChildEvent, opt => opt.Ignore());

            CreateMap<VaccinationEvent, VaccinationEventListItemDto>()
                .ForMember(dest => dest.VaccineName,
                    opt => opt.MapFrom(src => src.Vaccine != null ? src.Vaccine.Name : null))
                .ForMember(dest => dest.DoseType,
                    opt => opt.MapFrom(src => new EnumValueDto
                    {
                        Value = (int)src.DoseType,
                        Label = src.DoseType.GetDescription()
                    }))
                .ForMember(dest => dest.AnimalCount,
                    opt => opt.MapFrom(src => src.EventAnimals != null ? src.EventAnimals.Count : 0))
                .ForMember(dest => dest.Status, opt => opt.Ignore());

            CreateMap<VaccinationEventAnimal, VaccinationEventAnimalDto>()
                .ForMember(dest => dest.Name,
                    opt => opt.MapFrom(src => src.Animal != null ? src.Animal.Name : null))
                .ForMember(dest => dest.TagNumber,
                    opt => opt.MapFrom(src => src.Animal != null ? src.Animal.TagNumber : null));

            CreateMap<VaccinationEvent, VaccinationHistoryItemDto>()
                .ForMember(dest => dest.VaccinationEventId, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.VaccineName,
                    opt => opt.MapFrom(src => src.Vaccine != null ? src.Vaccine.Name : null))
                .ForMember(dest => dest.ApplicationDate,
                    opt => opt.MapFrom(src => src.ApplicationDate!.Value))
                .ForMember(dest => dest.DoseType,
                    opt => opt.MapFrom(src => new EnumValueDto
                    {
                        Value = (int)src.DoseType,
                        Label = src.DoseType.GetDescription()
                    }));
        }
    }
}
