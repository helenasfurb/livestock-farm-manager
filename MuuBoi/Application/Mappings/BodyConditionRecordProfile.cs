using AutoMapper;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Helpers;
using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Mappings
{
    public class BodyConditionRecordProfile : Profile
    {
        public BodyConditionRecordProfile()
        {
            CreateMap<BodyConditionRecord, BodyConditionRecordDto>()
                .ForMember(dest => dest.ScoreLabel,
                    opt => opt.MapFrom(src => src.Score.GetDescription()));
        }
    }
}
