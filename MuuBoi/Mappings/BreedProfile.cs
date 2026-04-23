using MuuBoi.DTOs;
using MuuBoi.Models;
using AutoMapper;

namespace MuuBoi.Mappings
{
    public class BreedProfile : Profile
    {
        public BreedProfile()
        {
            CreateMap<Breed, BreedDto>();

            CreateMap<Breed, BreedResponseDto>();

            CreateMap<BreedCreateDto, Breed>()
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow))
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(_ => true))
                .ForMember(dest => dest.UserId, opt => opt.Ignore())
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore());

            CreateMap<BreedUpdateDto, Breed>()
                .ForAllMembers(opt => opt.Condition((_, _, srcMember) => srcMember != null));
        }
        
    }
}
